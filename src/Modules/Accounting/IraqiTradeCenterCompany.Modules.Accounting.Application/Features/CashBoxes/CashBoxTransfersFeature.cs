using IraqiTradeCenterCompany.Modules.Accounting.Application.Internal;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.CashBoxes;

// ─────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────

public record CashBoxTransferDto(
    int Id,
    string TransferNumber,
    int FromCashBoxId,
    string FromCashBoxCode,
    string FromCashBoxName,
    int ToCashBoxId,
    string ToCashBoxCode,
    string ToCashBoxName,
    int TransitAccountId,
    string? TransitAccountCode,
    string? TransitAccountName,
    string Currency,
    decimal Amount,
    DateTime SendDate,
    DateTime ReceiveDate,
    string? Description,
    string? ReferenceNumber,
    int SendJournalEntryId,
    string? SendEntryNumber,
    int? ReceiveJournalEntryId,
    string? ReceiveEntryNumber,
    int? ReversalJournalEntryId,
    string? ReversalEntryNumber,
    string Status,
    string? ReceivedByUserId,
    DateTime? ReceivedAt,
    string? ReceiveNotes,
    string? CancelledByUserId,
    DateTime? CancelledAt,
    string? CancellationReason,
    DateTime CreatedAt
);

public record CashBoxBalanceDto(
    int CashBoxId,
    string Code,
    string NameAr,
    int AccountId,
    string? AccountCode,
    string? AccountName,
    string Currency,
    decimal Debit,
    decimal Credit,
    decimal Balance,
    decimal? DebitLimit,
    decimal? CreditLimit
);

public record CreateCashBoxTransferDto(
    int FromCashBoxId,
    int ToCashBoxId,
    int TransitAccountId,
    string Currency,
    decimal Amount,
    DateTime SendDate,
    DateTime ReceiveDate,
    string? Description,
    string? ReferenceNumber,
    bool PostImmediately = true
);

/// <summary>تأكيد استلام مناقلة من قِبَل أمين الصندوق المستلم.</summary>
public record ReceiveCashBoxTransferDto(
    DateTime? ActualReceiveDate,
    string? Notes,
    bool PostImmediately = true
);

public record CancelCashBoxTransferDto(
    string? Reason,
    DateTime? ReversalDate,
    bool PostImmediately = true
);

/// <summary>
/// التراجع عن استلام مناقلة سبق وأكَّدها أمين الصندوق المستلم — يولِّد قيد
/// عكس للاستلام (يُخصَم من الصندوق المستلم ويُعاد للحساب الوسيط)، وتُعاد
/// المناقلة إلى حالة "بانتظار الاستلام" حتى يستطيع المُرسِل التعديل.
/// </summary>
public record UnreceiveCashBoxTransferDto(
    string? Reason,
    DateTime? ReversalDate,
    bool PostImmediately = true
);

/// <summary>
/// تعديل بيانات مناقلة بانتظار الاستلام (يُعاد توليد قيد الإرسال بالقيم الجديدة).
/// لا يُسمح بتعديل الصناديق أو العملة — هذه تستلزم إلغاء المناقلة وإنشاء جديدة.
/// </summary>
public record UpdateCashBoxTransferDto(
    decimal Amount,
    DateTime SendDate,
    int TransitAccountId,
    string? Description,
    string? ReferenceNumber,
    bool PostImmediately = true
);

/// <summary>
/// حذف مناقلة ملغاة نهائياً مع جميع القيود المحاسبية المرتبطة بها (الإرسال،
/// الاستلام إن وُجد، والقيود العكسية). متاح فقط للمناقلات في حالة "ملغاة"
/// لأن قيود الإلغاء هي العكس الحسابي لقيد الإرسال فيُلغى الأثر بحذف الزوج.
/// </summary>
public record DeleteCashBoxTransferDto(string? Reason);

// ─────────────────────────────────────────────────────────────────────
// Queries
// ─────────────────────────────────────────────────────────────────────

public record GetCashBoxBalancesQuery(string? Currency = null) : IRequest<List<CashBoxBalanceDto>>;

public class GetCashBoxBalancesHandler : IRequestHandler<GetCashBoxBalancesQuery, List<CashBoxBalanceDto>>
{
    private readonly IAccountingDbContext _db;
    public GetCashBoxBalancesHandler(IAccountingDbContext db) => _db = db;

    public async Task<List<CashBoxBalanceDto>> Handle(GetCashBoxBalancesQuery req, CancellationToken ct)
    {
        var boxes = await _db.CashBoxes.AsNoTracking()
            .Include(b => b.Account)
            .Include(b => b.Currencies)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.Code)
            .ToListAsync(ct);

        var accountIds = boxes.Select(b => b.AccountId).Distinct().ToList();
        if (accountIds.Count == 0) return new List<CashBoxBalanceDto>();

        var ledger = await (from l in _db.JournalEntryLines.AsNoTracking()
                            join e in _db.JournalEntries.AsNoTracking() on l.JournalEntryId equals e.Id
                            where accountIds.Contains(l.AccountId)
                                  && !l.IsDeleted && !e.IsDeleted
                                  && e.Status == JournalEntryStatus.Posted
                            select new { l.AccountId, e.Currency, l.IsDebit, l.Amount })
            .GroupBy(x => new { x.AccountId, x.Currency })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.Currency,
                Debit = g.Where(x => x.IsDebit).Sum(x => (decimal?)x.Amount) ?? 0m,
                Credit = g.Where(x => !x.IsDebit).Sum(x => (decimal?)x.Amount) ?? 0m,
            })
            .ToListAsync(ct);

        var filterCur = string.IsNullOrWhiteSpace(req.Currency) ? null : req.Currency.Trim().ToUpperInvariant();

        var result = new List<CashBoxBalanceDto>();
        foreach (var box in boxes)
        {
            var declaredCurrencies = box.Currencies
                .Where(c => c.IsActive)
                .Select(c => c.Currency)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var ledgerCurrencies = ledger
                .Where(l => l.AccountId == box.AccountId)
                .Select(l => l.Currency)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var allCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            allCurrencies.UnionWith(declaredCurrencies);
            allCurrencies.UnionWith(ledgerCurrencies);
            if (allCurrencies.Count == 0) allCurrencies.Add("IQD");

            foreach (var cur in allCurrencies.OrderBy(c => c))
            {
                if (filterCur != null && !string.Equals(cur, filterCur, StringComparison.OrdinalIgnoreCase))
                    continue;

                var row = ledger.FirstOrDefault(l => l.AccountId == box.AccountId
                    && string.Equals(l.Currency, cur, StringComparison.OrdinalIgnoreCase));
                var d = row?.Debit ?? 0m;
                var c = row?.Credit ?? 0m;
                var cbCur = box.Currencies.FirstOrDefault(x =>
                    string.Equals(x.Currency, cur, StringComparison.OrdinalIgnoreCase));

                result.Add(new CashBoxBalanceDto(
                    box.Id,
                    box.Code,
                    box.NameAr,
                    box.AccountId,
                    box.Account?.Code,
                    box.Account?.NameAr,
                    cur.ToUpperInvariant(),
                    d,
                    c,
                    d - c,
                    cbCur?.DebitLimit,
                    cbCur?.CreditLimit
                ));
            }
        }

        return result;
    }
}

public record GetCashBoxTransfersQuery(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int? CashBoxId = null,
    string? Currency = null,
    string? Status = null,
    int Skip = 0,
    int Take = 100
) : IRequest<List<CashBoxTransferDto>>;

public class GetCashBoxTransfersHandler : IRequestHandler<GetCashBoxTransfersQuery, List<CashBoxTransferDto>>
{
    private readonly IAccountingDbContext _db;
    public GetCashBoxTransfersHandler(IAccountingDbContext db) => _db = db;

    public async Task<List<CashBoxTransferDto>> Handle(GetCashBoxTransfersQuery req, CancellationToken ct)
    {
        var q = _db.CashBoxTransfers.AsNoTracking()
            .Include(t => t.FromCashBox)
            .Include(t => t.ToCashBox)
            .Include(t => t.TransitAccount)
            .Include(t => t.SendJournalEntry)
            .Include(t => t.ReceiveJournalEntry)
            .Include(t => t.ReversalJournalEntry)
            .AsQueryable();

        if (req.FromDate.HasValue)
        {
            var f = req.FromDate.Value.Date;
            q = q.Where(t => t.SendDate >= f);
        }
        if (req.ToDate.HasValue)
        {
            var to = req.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            q = q.Where(t => t.SendDate <= to);
        }
        if (req.CashBoxId.HasValue)
        {
            var id = req.CashBoxId.Value;
            q = q.Where(t => t.FromCashBoxId == id || t.ToCashBoxId == id);
        }
        if (!string.IsNullOrWhiteSpace(req.Currency))
        {
            var cur = req.Currency.Trim().ToUpperInvariant();
            q = q.Where(t => t.Currency == cur);
        }
        if (!string.IsNullOrWhiteSpace(req.Status)
            && Enum.TryParse<CashBoxTransferStatus>(req.Status, ignoreCase: true, out var st))
        {
            q = q.Where(t => t.Status == st);
        }

        var skip = Math.Max(0, req.Skip);
        var take = Math.Clamp(req.Take, 1, 500);

        var rows = await q
            .OrderByDescending(t => t.SendDate)
            .ThenByDescending(t => t.Id)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        return rows.Select(t => new CashBoxTransferDto(
            t.Id,
            t.TransferNumber,
            t.FromCashBoxId,
            t.FromCashBox?.Code ?? "—",
            t.FromCashBox?.NameAr ?? "—",
            t.ToCashBoxId,
            t.ToCashBox?.Code ?? "—",
            t.ToCashBox?.NameAr ?? "—",
            t.TransitAccountId,
            t.TransitAccount?.Code,
            t.TransitAccount?.NameAr,
            t.Currency,
            t.Amount,
            t.SendDate,
            t.ReceiveDate,
            t.Description,
            t.ReferenceNumber,
            t.SendJournalEntryId,
            t.SendJournalEntry?.EntryNumber,
            t.ReceiveJournalEntryId,
            t.ReceiveJournalEntry?.EntryNumber,
            t.ReversalJournalEntryId,
            t.ReversalJournalEntry?.EntryNumber,
            t.Status.ToString(),
            t.ReceivedByUserId,
            t.ReceivedAt,
            t.ReceiveNotes,
            t.CancelledByUserId,
            t.CancelledAt,
            t.CancellationReason,
            t.CreatedAt
        )).ToList();
    }
}

// ─────────────────────────────────────────────────────────────────────
// Command: إنشاء مناقلة (يولِّد قيد الإرسال فقط — قيد الاستلام ينتظر الموافقة)
// ─────────────────────────────────────────────────────────────────────

public record CreateCashBoxTransferCommand(CreateCashBoxTransferDto Data) : IRequest<Result<int>>;

public class CreateCashBoxTransferHandler : IRequestHandler<CreateCashBoxTransferCommand, Result<int>>
{
    private readonly IAccountingDbContext _db;
    private readonly IPeriodResolver _periods;
    private readonly ICurrentUserService _currentUser;

    public CreateCashBoxTransferHandler(
        IAccountingDbContext db,
        IPeriodResolver periods,
        ICurrentUserService currentUser)
    {
        _db = db;
        _periods = periods;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(CreateCashBoxTransferCommand req, CancellationToken ct)
    {
        try
        {
            var d = req.Data;

            if (d.FromCashBoxId == d.ToCashBoxId)
                return Result.Failure<int>("لا يمكن المناقلة بين الصندوق ونفسه — اختر صندوقاً مختلفاً.");
            if (d.Amount <= 0)
                return Result.Failure<int>("مبلغ المناقلة يجب أن يكون أكبر من صفر.");
            if (d.ReceiveDate.Date < d.SendDate.Date)
                return Result.Failure<int>("تاريخ الاستلام المتوقَّع لا يمكن أن يسبق تاريخ الإرسال.");

            var cur = string.IsNullOrWhiteSpace(d.Currency) ? "IQD" : d.Currency.Trim().ToUpperInvariant();

            var fromBox = await _db.CashBoxes.AsNoTracking()
                .Include(b => b.Account)
                .Include(b => b.Currencies)
                .FirstOrDefaultAsync(b => b.Id == d.FromCashBoxId, ct);
            if (fromBox == null) return Result.Failure<int>("الصندوق المُرسِل غير موجود.");
            if (!fromBox.IsActive) return Result.Failure<int>($"الصندوق المُرسِل '{fromBox.NameAr}' معطّل.");

            var toBox = await _db.CashBoxes.AsNoTracking()
                .Include(b => b.Account)
                .Include(b => b.Currencies)
                .FirstOrDefaultAsync(b => b.Id == d.ToCashBoxId, ct);
            if (toBox == null) return Result.Failure<int>("الصندوق المستلم غير موجود.");
            if (!toBox.IsActive) return Result.Failure<int>($"الصندوق المستلم '{toBox.NameAr}' معطّل.");

            var fromSupports = fromBox.Currencies.Any(c => c.IsActive
                && string.Equals(c.Currency, cur, StringComparison.OrdinalIgnoreCase));
            if (!fromSupports)
                return Result.Failure<int>($"الصندوق '{fromBox.NameAr}' لا يدعم العملة {cur}.");
            var toSupports = toBox.Currencies.Any(c => c.IsActive
                && string.Equals(c.Currency, cur, StringComparison.OrdinalIgnoreCase));
            if (!toSupports)
                return Result.Failure<int>($"الصندوق '{toBox.NameAr}' لا يدعم العملة {cur}.");

            var transitAcc = await _db.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == d.TransitAccountId, ct);
            if (transitAcc == null) return Result.Failure<int>("الحساب الوسيط غير موجود.");
            if (!transitAcc.IsActive) return Result.Failure<int>($"الحساب الوسيط '{transitAcc.NameAr}' معطّل.");
            if (!transitAcc.IsLeaf) return Result.Failure<int>($"الحساب الوسيط '{transitAcc.NameAr}' حساب رئيسي — اختر حساباً فرعياً.");

            var isTransitLinkedToBox = await _db.CashBoxes.AsNoTracking()
                .AnyAsync(b => b.AccountId == d.TransitAccountId, ct);
            if (isTransitLinkedToBox)
                return Result.Failure<int>(
                    $"الحساب الوسيط '{transitAcc.NameAr}' مرتبط بصندوق آخر — استخدم حساباً وسيطاً مستقلاً (مثل: نقدية تحت التحويل).");

            var activeFy = await _db.FiscalYears.AsNoTracking()
                .FirstOrDefaultAsync(f => f.IsActive, ct);
            if (activeFy != null)
            {
                if (d.SendDate.Date < activeFy.StartDate.Date || d.SendDate.Date > activeFy.EndDate.Date)
                    return Result.Failure<int>(
                        $"تاريخ الإرسال ({d.SendDate:yyyy-MM-dd}) خارج السنة المالية النشطة '{activeFy.Name}'.");
            }

            await using var tx = await _db.BeginTransactionAsync(ct);

            var ctOut = await EnsureSystemVoucherTypeAsync("CT-OUT", "تحويل صادر", "Cash Transfer Out",
                VoucherNature.Credit, displayOrder: 200, ct);

            // ‎فحص سقف الصندوق المُرسِل (قيد الإرسال فقط الآن)
            var fromCheck = await CashBoxGuard.ValidateAsync(_db,
                new[] {
                    new CashBoxGuard.LineSnapshot(fromBox.AccountId, IsDebit: false, d.Amount),
                    new CashBoxGuard.LineSnapshot(d.TransitAccountId, IsDebit: true, d.Amount),
                },
                cur, ctOut.Id, excludeJournalEntryId: null, ct);
            if (fromCheck != null) return Result.Failure<int>(fromCheck);

            var sendCurCheck = await EnsureCurrencyHasActiveBulletin(cur, d.SendDate, ct);
            if (sendCurCheck != null) return Result.Failure<int>(sendCurCheck);

            var transferNumber = await NextTransferNumberAsync(ct);
            var refLabel = $"مناقلة {transferNumber}: {fromBox.NameAr} ⇒ {toBox.NameAr}";

            var sendEntry = await BuildAndAddJournalEntryAsync(
                date: d.SendDate,
                description: $"{refLabel} — إرسال" + (string.IsNullOrWhiteSpace(d.Description) ? "" : $" ({d.Description})"),
                voucherTypeId: ctOut.Id,
                currency: cur,
                lines: new (int AccountId, bool IsDebit, decimal Amount, string? Desc)[]
                {
                    (d.TransitAccountId, true,  d.Amount, $"تحت التحويل إلى {toBox.NameAr}"),
                    (fromBox.AccountId,  false, d.Amount, $"إخراج من {fromBox.NameAr}"),
                },
                referenceType: "CashBoxTransfer",
                referenceNumber: transferNumber,
                postImmediately: d.PostImmediately,
                ct: ct);

            var transfer = CashBoxTransfer.Create(
                transferNumber,
                fromBox.Id, toBox.Id,
                d.TransitAccountId,
                cur, d.Amount,
                d.SendDate, d.ReceiveDate,
                sendEntry.Id,
                d.Description, d.ReferenceNumber);

            await _db.CashBoxTransfers.AddAsync(transfer, ct);
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            return Result.Success(transfer.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }

    private async Task<JournalVoucherType> EnsureSystemVoucherTypeAsync(
        string code, string nameAr, string? nameEn, VoucherNature nature, int displayOrder, CancellationToken ct)
    {
        var existing = await _db.JournalVoucherTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Code == code, ct);
        if (existing != null)
        {
            if (existing.IsDeleted)
            {
                existing.Restore();
                if (!existing.IsEnabled) existing.SetEnabled(true);
            }
            return existing;
        }
        var vt = JournalVoucherType.Create(
            code: code,
            nameAr: nameAr,
            nameEn: nameEn,
            description: "نوع سند نظامي للمناقلات بين الصناديق",
            isEnabled: true,
            isSystem: true,
            displayOrder: displayOrder,
            nature: nature,
            showInSidebar: false);
        await _db.JournalVoucherTypes.AddAsync(vt, ct);
        await _db.SaveChangesAsync(ct);
        return vt;
    }

    private async Task<string> NextTransferNumberAsync(CancellationToken ct)
    {
        var maxNum = await _db.CashBoxTransfers.IgnoreQueryFilters()
            .Where(t => t.TransferNumber.StartsWith("TRF-"))
            .Select(t => t.TransferNumber)
            .ToListAsync(ct);

        var maxN = 0;
        foreach (var s in maxNum)
        {
            var rest = s.Substring(4);
            if (int.TryParse(rest, out var n) && n > maxN) maxN = n;
        }
        return $"TRF-{maxN + 1}";
    }

    private async Task<JournalEntry> BuildAndAddJournalEntryAsync(
        DateTime date,
        string description,
        int voucherTypeId,
        string currency,
        IReadOnlyList<(int AccountId, bool IsDebit, decimal Amount, string? Desc)> lines,
        string? referenceType,
        string? referenceNumber,
        bool postImmediately,
        CancellationToken ct)
    {
        var (fyId, periodId) = await _periods.ResolveAsync(date, ct);
        var nextNum = await _db.GetNextJournalEntryNumberAsync(fyId, ct);
        var voucherSeq = await _db.GetNextVoucherSequenceAsync(voucherTypeId, ct);

        var entry = JournalEntry.Create(
            date: date,
            fyId: fyId,
            periodId: periodId,
            source: JournalEntrySource.System,
            description: description,
            refType: referenceType,
            refId: null,
            refNumber: referenceNumber,
            type: JournalEntryType.Normal,
            currency: currency,
            entryNumber: nextNum.ToString(),
            voucherTypeId: voucherTypeId,
            voucherSequence: voucherSeq);

        foreach (var l in lines)
        {
            if (l.IsDebit) entry.AddDebit(l.AccountId, l.Amount, l.Desc);
            else entry.AddCredit(l.AccountId, l.Amount, l.Desc);
        }

        const string VoucherPostPermission = "Accounting.Vouchers.Post";
        var canPost = _currentUser.IsSuperAdmin || _currentUser.HasPermission(VoucherPostPermission);
        if (postImmediately && canPost)
            entry.Post(_currentUser.UserId?.ToString() ?? "system");

        await _db.JournalEntries.AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    private async Task<string?> EnsureCurrencyHasActiveBulletin(string currency, DateTime entryDate, CancellationToken ct)
    {
        var cur = (currency ?? "IQD").Trim().ToUpperInvariant();
        var atUtc = (entryDate.Kind == DateTimeKind.Utc ? entryDate : entryDate.ToUniversalTime())
            .Date.AddDays(1).AddTicks(-1);

        var bulletin = await _db.CurrencyRateBulletins
            .Include(b => b.Lines)
            .Where(b => b.Status == CurrencyRateBulletinStatus.Published && b.EffectiveAt <= atUtc)
            .OrderByDescending(b => b.EffectiveAt).ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

        if (bulletin != null && string.Equals(bulletin.BaseCurrency, cur, StringComparison.OrdinalIgnoreCase))
            return null;
        if (bulletin == null)
        {
            if (cur == "IQD") return null;
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار — لا توجد نشرة منشورة سارية بتاريخ {entryDate:yyyy-MM-dd}.";
        }
        var hasLine = bulletin.Lines.Any(l => string.Equals(l.Currency, cur, StringComparison.OrdinalIgnoreCase));
        if (!hasLine)
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار '{bulletin.Name}' — أضف سعر صرف لها أو أصدر نشرة جديدة.";
        return null;
    }
}

// ─────────────────────────────────────────────────────────────────────
// Command: تأكيد استلام مناقلة (يولِّد قيد الاستلام الآن)
// ─────────────────────────────────────────────────────────────────────

public record ReceiveCashBoxTransferCommand(int TransferId, ReceiveCashBoxTransferDto Data)
    : IRequest<Result<int>>;

public class ReceiveCashBoxTransferHandler : IRequestHandler<ReceiveCashBoxTransferCommand, Result<int>>
{
    private readonly IAccountingDbContext _db;
    private readonly IPeriodResolver _periods;
    private readonly ICurrentUserService _currentUser;

    public ReceiveCashBoxTransferHandler(
        IAccountingDbContext db,
        IPeriodResolver periods,
        ICurrentUserService currentUser)
    {
        _db = db;
        _periods = periods;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(ReceiveCashBoxTransferCommand req, CancellationToken ct)
    {
        try
        {
            var transfer = await _db.CashBoxTransfers
                .Include(t => t.FromCashBox)
                .Include(t => t.ToCashBox)
                .Include(t => t.TransitAccount)
                .FirstOrDefaultAsync(t => t.Id == req.TransferId, ct);
            if (transfer == null)
                return Result.Failure<int>("المناقلة غير موجودة.");
            if (transfer.Status != CashBoxTransferStatus.PendingReceive)
                return Result.Failure<int>(
                    $"لا يمكن استلام مناقلة حالتها '{transfer.Status}' — يُستَلَم فقط ما هو 'بانتظار الاستلام'.");

            var actualReceiveDate = req.Data.ActualReceiveDate ?? DateTime.Now;
            if (actualReceiveDate.Date < transfer.SendDate.Date)
                return Result.Failure<int>("تاريخ الاستلام الفعلي لا يمكن أن يسبق تاريخ الإرسال.");

            // ‎التاريخ ضمن السنة المالية النشطة
            var activeFy = await _db.FiscalYears.AsNoTracking()
                .FirstOrDefaultAsync(f => f.IsActive, ct);
            if (activeFy != null && (actualReceiveDate.Date < activeFy.StartDate.Date
                || actualReceiveDate.Date > activeFy.EndDate.Date))
            {
                return Result.Failure<int>(
                    $"تاريخ الاستلام ({actualReceiveDate:yyyy-MM-dd}) خارج السنة المالية النشطة '{activeFy.Name}'.");
            }

            var toBox = transfer.ToCashBox!;
            if (!toBox.IsActive)
                return Result.Failure<int>($"الصندوق المستلم '{toBox.NameAr}' معطّل — لا يمكن استلام مناقلة فيه.");

            await using var tx = await _db.BeginTransactionAsync(ct);

            var ctIn = await EnsureSystemVoucherTypeAsync("CT-IN", "تحويل وارد", "Cash Transfer In",
                VoucherNature.Debit, displayOrder: 201, ct);

            var toCheck = await CashBoxGuard.ValidateAsync(_db,
                new[] {
                    new CashBoxGuard.LineSnapshot(toBox.AccountId, IsDebit: true, transfer.Amount),
                    new CashBoxGuard.LineSnapshot(transfer.TransitAccountId, IsDebit: false, transfer.Amount),
                },
                transfer.Currency, ctIn.Id, excludeJournalEntryId: null, ct);
            if (toCheck != null) return Result.Failure<int>(toCheck);

            var recvCurCheck = await EnsureCurrencyHasActiveBulletin(transfer.Currency, actualReceiveDate, ct);
            if (recvCurCheck != null) return Result.Failure<int>(recvCurCheck);

            var refLabel = $"مناقلة {transfer.TransferNumber}: {transfer.FromCashBox?.NameAr ?? "—"} ⇒ {toBox.NameAr}";

            var recvEntry = await BuildAndAddJournalEntryAsync(
                date: actualReceiveDate,
                description: $"{refLabel} — استلام" + (string.IsNullOrWhiteSpace(req.Data.Notes) ? "" : $" ({req.Data.Notes})"),
                voucherTypeId: ctIn.Id,
                currency: transfer.Currency,
                lines: new (int AccountId, bool IsDebit, decimal Amount, string? Desc)[]
                {
                    (toBox.AccountId,            true,  transfer.Amount, $"إيداع في {toBox.NameAr}"),
                    (transfer.TransitAccountId,  false, transfer.Amount, $"إغلاق التحويل من {transfer.FromCashBox?.NameAr ?? "—"}"),
                },
                referenceType: "CashBoxTransfer",
                referenceNumber: transfer.TransferNumber,
                postImmediately: req.Data.PostImmediately,
                ct: ct);

            var userIdStr = _currentUser.UserId?.ToString() ?? "system";
            transfer.MarkReceived(recvEntry.Id, actualReceiveDate, userIdStr, req.Data.Notes);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success(recvEntry.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }

    private async Task<JournalVoucherType> EnsureSystemVoucherTypeAsync(
        string code, string nameAr, string? nameEn, VoucherNature nature, int displayOrder, CancellationToken ct)
    {
        var existing = await _db.JournalVoucherTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Code == code, ct);
        if (existing != null)
        {
            if (existing.IsDeleted) { existing.Restore(); if (!existing.IsEnabled) existing.SetEnabled(true); }
            return existing;
        }
        var vt = JournalVoucherType.Create(
            code: code, nameAr: nameAr, nameEn: nameEn,
            description: "نوع سند نظامي للمناقلات بين الصناديق",
            isEnabled: true, isSystem: true, displayOrder: displayOrder,
            nature: nature, showInSidebar: false);
        await _db.JournalVoucherTypes.AddAsync(vt, ct);
        await _db.SaveChangesAsync(ct);
        return vt;
    }

    private async Task<JournalEntry> BuildAndAddJournalEntryAsync(
        DateTime date, string description, int voucherTypeId, string currency,
        IReadOnlyList<(int AccountId, bool IsDebit, decimal Amount, string? Desc)> lines,
        string? referenceType, string? referenceNumber, bool postImmediately, CancellationToken ct)
    {
        var (fyId, periodId) = await _periods.ResolveAsync(date, ct);
        var nextNum = await _db.GetNextJournalEntryNumberAsync(fyId, ct);
        var voucherSeq = await _db.GetNextVoucherSequenceAsync(voucherTypeId, ct);

        var entry = JournalEntry.Create(
            date: date, fyId: fyId, periodId: periodId,
            source: JournalEntrySource.System, description: description,
            refType: referenceType, refId: null, refNumber: referenceNumber,
            type: JournalEntryType.Normal, currency: currency,
            entryNumber: nextNum.ToString(),
            voucherTypeId: voucherTypeId, voucherSequence: voucherSeq);

        foreach (var l in lines)
        {
            if (l.IsDebit) entry.AddDebit(l.AccountId, l.Amount, l.Desc);
            else entry.AddCredit(l.AccountId, l.Amount, l.Desc);
        }

        const string VoucherPostPermission = "Accounting.Vouchers.Post";
        var canPost = _currentUser.IsSuperAdmin || _currentUser.HasPermission(VoucherPostPermission);
        if (postImmediately && canPost)
            entry.Post(_currentUser.UserId?.ToString() ?? "system");

        await _db.JournalEntries.AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    private async Task<string?> EnsureCurrencyHasActiveBulletin(string currency, DateTime entryDate, CancellationToken ct)
    {
        var cur = (currency ?? "IQD").Trim().ToUpperInvariant();
        var atUtc = (entryDate.Kind == DateTimeKind.Utc ? entryDate : entryDate.ToUniversalTime())
            .Date.AddDays(1).AddTicks(-1);

        var bulletin = await _db.CurrencyRateBulletins
            .Include(b => b.Lines)
            .Where(b => b.Status == CurrencyRateBulletinStatus.Published && b.EffectiveAt <= atUtc)
            .OrderByDescending(b => b.EffectiveAt).ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

        if (bulletin != null && string.Equals(bulletin.BaseCurrency, cur, StringComparison.OrdinalIgnoreCase))
            return null;
        if (bulletin == null)
        {
            if (cur == "IQD") return null;
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار — لا توجد نشرة منشورة سارية بتاريخ {entryDate:yyyy-MM-dd}.";
        }
        var hasLine = bulletin.Lines.Any(l => string.Equals(l.Currency, cur, StringComparison.OrdinalIgnoreCase));
        if (!hasLine)
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار '{bulletin.Name}' — أضف سعر صرف لها أو أصدر نشرة جديدة.";
        return null;
    }
}

// ─────────────────────────────────────────────────────────────────────
// Command: إلغاء مناقلة قبل الاستلام (يولِّد قيد عكس الإرسال)
// ─────────────────────────────────────────────────────────────────────

public record CancelCashBoxTransferCommand(int TransferId, CancelCashBoxTransferDto Data)
    : IRequest<Result<int>>;

public class CancelCashBoxTransferHandler : IRequestHandler<CancelCashBoxTransferCommand, Result<int>>
{
    private readonly IAccountingDbContext _db;
    private readonly IPeriodResolver _periods;
    private readonly ICurrentUserService _currentUser;

    public CancelCashBoxTransferHandler(
        IAccountingDbContext db,
        IPeriodResolver periods,
        ICurrentUserService currentUser)
    {
        _db = db;
        _periods = periods;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(CancelCashBoxTransferCommand req, CancellationToken ct)
    {
        try
        {
            var transfer = await _db.CashBoxTransfers
                .Include(t => t.FromCashBox)
                .Include(t => t.ToCashBox)
                .Include(t => t.TransitAccount)
                .Include(t => t.SendJournalEntry)
                    .ThenInclude(e => e!.Lines)
                .FirstOrDefaultAsync(t => t.Id == req.TransferId, ct);
            if (transfer == null) return Result.Failure<int>("المناقلة غير موجودة.");
            if (transfer.Status != CashBoxTransferStatus.PendingReceive)
                return Result.Failure<int>(
                    $"لا يمكن إلغاء مناقلة حالتها '{transfer.Status}' — يُلغى فقط 'بانتظار الاستلام'.");

            var reversalDate = req.Data.ReversalDate ?? DateTime.Now;
            if (reversalDate.Date < transfer.SendDate.Date)
                reversalDate = transfer.SendDate;

            var activeFy = await _db.FiscalYears.AsNoTracking()
                .FirstOrDefaultAsync(f => f.IsActive, ct);
            if (activeFy != null && (reversalDate.Date < activeFy.StartDate.Date
                || reversalDate.Date > activeFy.EndDate.Date))
            {
                return Result.Failure<int>(
                    $"تاريخ العكس ({reversalDate:yyyy-MM-dd}) خارج السنة المالية النشطة '{activeFy.Name}'.");
            }

            await using var tx = await _db.BeginTransactionAsync(ct);

            var ctOut = await EnsureSystemVoucherTypeAsync("CT-OUT", "تحويل صادر", "Cash Transfer Out",
                VoucherNature.Credit, displayOrder: 200, ct);

            var refLabel = $"عكس مناقلة {transfer.TransferNumber}: " +
                $"{transfer.FromCashBox?.NameAr ?? "—"} ⇒ {transfer.ToCashBox?.NameAr ?? "—"}";
            var reason = string.IsNullOrWhiteSpace(req.Data.Reason) ? "إلغاء قبل الاستلام" : req.Data.Reason!;

            // ‎عكس قيد الإرسال: المُرسِل مدين، الحساب الوسيط دائن
            var revEntry = await BuildAndAddJournalEntryAsync(
                date: reversalDate,
                description: $"{refLabel} — {reason}",
                voucherTypeId: ctOut.Id,
                currency: transfer.Currency,
                lines: new (int AccountId, bool IsDebit, decimal Amount, string? Desc)[]
                {
                    (transfer.FromCashBox!.AccountId, true,  transfer.Amount, $"إعادة إلى {transfer.FromCashBox!.NameAr}"),
                    (transfer.TransitAccountId,        false, transfer.Amount, "إغلاق الحساب الوسيط (إلغاء)"),
                },
                referenceType: "CashBoxTransferReversal",
                referenceNumber: transfer.TransferNumber,
                postImmediately: req.Data.PostImmediately,
                ct: ct);

            var userIdStr = _currentUser.UserId?.ToString() ?? "system";
            transfer.Cancel(revEntry.Id, userIdStr, req.Data.Reason);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success(revEntry.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }

    private async Task<JournalVoucherType> EnsureSystemVoucherTypeAsync(
        string code, string nameAr, string? nameEn, VoucherNature nature, int displayOrder, CancellationToken ct)
    {
        var existing = await _db.JournalVoucherTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Code == code, ct);
        if (existing != null)
        {
            if (existing.IsDeleted) { existing.Restore(); if (!existing.IsEnabled) existing.SetEnabled(true); }
            return existing;
        }
        var vt = JournalVoucherType.Create(
            code: code, nameAr: nameAr, nameEn: nameEn,
            description: "نوع سند نظامي للمناقلات بين الصناديق",
            isEnabled: true, isSystem: true, displayOrder: displayOrder,
            nature: nature, showInSidebar: false);
        await _db.JournalVoucherTypes.AddAsync(vt, ct);
        await _db.SaveChangesAsync(ct);
        return vt;
    }

    private async Task<JournalEntry> BuildAndAddJournalEntryAsync(
        DateTime date, string description, int voucherTypeId, string currency,
        IReadOnlyList<(int AccountId, bool IsDebit, decimal Amount, string? Desc)> lines,
        string? referenceType, string? referenceNumber, bool postImmediately, CancellationToken ct)
    {
        var (fyId, periodId) = await _periods.ResolveAsync(date, ct);
        var nextNum = await _db.GetNextJournalEntryNumberAsync(fyId, ct);
        var voucherSeq = await _db.GetNextVoucherSequenceAsync(voucherTypeId, ct);

        var entry = JournalEntry.Create(
            date: date, fyId: fyId, periodId: periodId,
            source: JournalEntrySource.System, description: description,
            refType: referenceType, refId: null, refNumber: referenceNumber,
            type: JournalEntryType.Normal, currency: currency,
            entryNumber: nextNum.ToString(),
            voucherTypeId: voucherTypeId, voucherSequence: voucherSeq);

        foreach (var l in lines)
        {
            if (l.IsDebit) entry.AddDebit(l.AccountId, l.Amount, l.Desc);
            else entry.AddCredit(l.AccountId, l.Amount, l.Desc);
        }

        const string VoucherPostPermission = "Accounting.Vouchers.Post";
        var canPost = _currentUser.IsSuperAdmin || _currentUser.HasPermission(VoucherPostPermission);
        if (postImmediately && canPost)
            entry.Post(_currentUser.UserId?.ToString() ?? "system");

        await _db.JournalEntries.AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
        return entry;
    }
}

// ─────────────────────────────────────────────────────────────────────
// Command: التراجع عن استلام مناقلة (يولِّد قيد عكس للاستلام)
// تستخدم عندما يحتاج المُرسِل تعديلاً بعد أن وافق المستلم على الاستلام:
//   1) المستلم يتراجع عن الاستلام (يُعكس قيد الاستلام محاسبياً)
//   2) تعود المناقلة إلى حالة "بانتظار الاستلام"
//   3) عندئذ يستطيع المُرسِل الإلغاء أو إعادة الإرسال بقيمة جديدة
// مع التحقق من توفّر الرصيد لدى الصندوق المستلم لإجراء العكس.
// ─────────────────────────────────────────────────────────────────────

public record UnreceiveCashBoxTransferCommand(int TransferId, UnreceiveCashBoxTransferDto Data)
    : IRequest<Result<int>>;

public class UnreceiveCashBoxTransferHandler : IRequestHandler<UnreceiveCashBoxTransferCommand, Result<int>>
{
    private readonly IAccountingDbContext _db;
    private readonly IPeriodResolver _periods;
    private readonly ICurrentUserService _currentUser;

    public UnreceiveCashBoxTransferHandler(
        IAccountingDbContext db,
        IPeriodResolver periods,
        ICurrentUserService currentUser)
    {
        _db = db;
        _periods = periods;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(UnreceiveCashBoxTransferCommand req, CancellationToken ct)
    {
        try
        {
            var transfer = await _db.CashBoxTransfers
                .Include(t => t.FromCashBox)
                .Include(t => t.ToCashBox)
                .Include(t => t.TransitAccount)
                .Include(t => t.ReceiveJournalEntry)
                .FirstOrDefaultAsync(t => t.Id == req.TransferId, ct);
            if (transfer == null) return Result.Failure<int>("المناقلة غير موجودة.");
            if (transfer.Status != CashBoxTransferStatus.Received)
                return Result.Failure<int>(
                    $"لا يمكن التراجع عن استلام مناقلة حالتها '{transfer.Status}' — التراجع متاح فقط للمناقلات المستلَمة.");
            if (!transfer.ReceiveJournalEntryId.HasValue)
                return Result.Failure<int>("لا يوجد قيد استلام مرتبط بهذه المناقلة — أمر غير متوقَّع.");

            var toBox = transfer.ToCashBox!;

            var reversalDate = req.Data.ReversalDate ?? DateTime.Now;
            // ‎لا يُعكس قبل تاريخ الاستلام الأصلي (وإلا قد ندخل في فترة لم
            // ‎يكن المبلغ موجوداً فيها أصلاً في الصندوق المستلم).
            if (reversalDate.Date < transfer.ReceiveDate.Date)
                reversalDate = transfer.ReceiveDate;

            var activeFy = await _db.FiscalYears.AsNoTracking()
                .FirstOrDefaultAsync(f => f.IsActive, ct);
            if (activeFy != null && (reversalDate.Date < activeFy.StartDate.Date
                || reversalDate.Date > activeFy.EndDate.Date))
            {
                return Result.Failure<int>(
                    $"تاريخ التراجع ({reversalDate:yyyy-MM-dd}) خارج السنة المالية النشطة '{activeFy.Name}'.");
            }

            // ‎(1) فحص توفّر الرصيد لدى الصندوق المستلم بنفس عملة المناقلة:
            //     إذا كان رصيده الحالي (مدين − دائن) أقل من قيمة المناقلة فلا
            //     يُسمح بالتراجع لأن المبلغ صُرف بعد الاستلام، وعكسُه يجعل
            //     الصندوق سالباً وهو ما يخالف منطق الصناديق النقدية.
            var balanceCheck = await ValidateReceivingBoxHasEnoughAsync(
                toBox.AccountId, transfer.Currency, transfer.Amount, ct);
            if (balanceCheck != null) return Result.Failure<int>(balanceCheck);

            await using var tx = await _db.BeginTransactionAsync(ct);

            var ctIn = await EnsureSystemVoucherTypeAsync("CT-IN", "تحويل وارد", "Cash Transfer In",
                VoucherNature.Debit, displayOrder: 201, ct);

            // ‎(2) فحص سقوف الصندوق المستلم بعد العكس
            var reverseCheck = await CashBoxGuard.ValidateAsync(_db,
                new[] {
                    new CashBoxGuard.LineSnapshot(toBox.AccountId, IsDebit: false, transfer.Amount),
                    new CashBoxGuard.LineSnapshot(transfer.TransitAccountId, IsDebit: true, transfer.Amount),
                },
                transfer.Currency, ctIn.Id, excludeJournalEntryId: null, ct);
            if (reverseCheck != null) return Result.Failure<int>(reverseCheck);

            var refLabel = $"تراجع عن استلام مناقلة {transfer.TransferNumber}: " +
                $"{transfer.FromCashBox?.NameAr ?? "—"} ⇒ {toBox.NameAr}";
            var reason = string.IsNullOrWhiteSpace(req.Data.Reason)
                ? "تراجع عن الاستلام"
                : req.Data.Reason!;

            // ‎(3) قيد عكس الاستلام: الصندوق المستلم دائن، الحساب الوسيط مدين
            //     (يُعيد المبلغ إلى الحساب الوسيط ليصبح المُرسِل قادراً على
            //     الإلغاء أو إعادة الإرسال بمعطيات جديدة).
            var revEntry = await BuildAndAddJournalEntryAsync(
                date: reversalDate,
                description: $"{refLabel} — {reason}",
                voucherTypeId: ctIn.Id,
                currency: transfer.Currency,
                lines: new (int AccountId, bool IsDebit, decimal Amount, string? Desc)[]
                {
                    (transfer.TransitAccountId, true,  transfer.Amount, $"إعادة المبلغ تحت التحويل من {toBox.NameAr}"),
                    (toBox.AccountId,           false, transfer.Amount, $"عكس استلام في {toBox.NameAr}"),
                },
                referenceType: "CashBoxTransferReversal",
                referenceNumber: transfer.TransferNumber,
                postImmediately: req.Data.PostImmediately,
                ct: ct);

            // ‎(4) عكس قيد الاستلام الأصلي محاسبياً (تعليمه كمعكوس)
            if (transfer.ReceiveJournalEntry != null
                && transfer.ReceiveJournalEntry.Status == JournalEntryStatus.Posted)
            {
                transfer.ReceiveJournalEntry.MarkAsReversed(revEntry.Id);
            }

            // ‎(5) إعادة المناقلة إلى حالة "بانتظار الاستلام"
            transfer.Unreceive();

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success(revEntry.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }

    /// <summary>
    /// يتحقق أن الصندوق المستلم يملك ≥ amount من نفس العملة قبل السماح بالتراجع.
    /// </summary>
    private async Task<string?> ValidateReceivingBoxHasEnoughAsync(
        int receivingAccountId, string currency, decimal amount, CancellationToken ct)
    {
        var cur = (currency ?? "IQD").Trim().ToUpperInvariant();
        var balance = await (from l in _db.JournalEntryLines.AsNoTracking()
                             join e in _db.JournalEntries.AsNoTracking() on l.JournalEntryId equals e.Id
                             where l.AccountId == receivingAccountId
                                   && !l.IsDeleted && !e.IsDeleted
                                   && e.Status == JournalEntryStatus.Posted
                                   && e.Currency.ToUpper() == cur
                             select new { l.IsDebit, l.Amount })
            .ToListAsync(ct);

        var debit = balance.Where(x => x.IsDebit).Sum(x => x.Amount);
        var credit = balance.Where(x => !x.IsDebit).Sum(x => x.Amount);
        var net = debit - credit;

        if (net < amount)
            return $"رصيد الصندوق المستلم بالعملة {cur} ({net:N3}) أقلّ من قيمة المناقلة ({amount:N3}). " +
                   $"تعذَّر التراجع — أعد المبلغ إلى الصندوق أو راجع الحركات.";
        return null;
    }

    private async Task<JournalVoucherType> EnsureSystemVoucherTypeAsync(
        string code, string nameAr, string? nameEn, VoucherNature nature, int displayOrder, CancellationToken ct)
    {
        var existing = await _db.JournalVoucherTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Code == code, ct);
        if (existing != null)
        {
            if (existing.IsDeleted) { existing.Restore(); if (!existing.IsEnabled) existing.SetEnabled(true); }
            return existing;
        }
        var vt = JournalVoucherType.Create(
            code: code, nameAr: nameAr, nameEn: nameEn,
            description: "نوع سند نظامي للمناقلات بين الصناديق",
            isEnabled: true, isSystem: true, displayOrder: displayOrder,
            nature: nature, showInSidebar: false);
        await _db.JournalVoucherTypes.AddAsync(vt, ct);
        await _db.SaveChangesAsync(ct);
        return vt;
    }

    private async Task<JournalEntry> BuildAndAddJournalEntryAsync(
        DateTime date, string description, int voucherTypeId, string currency,
        IReadOnlyList<(int AccountId, bool IsDebit, decimal Amount, string? Desc)> lines,
        string? referenceType, string? referenceNumber, bool postImmediately, CancellationToken ct)
    {
        var (fyId, periodId) = await _periods.ResolveAsync(date, ct);
        var nextNum = await _db.GetNextJournalEntryNumberAsync(fyId, ct);
        var voucherSeq = await _db.GetNextVoucherSequenceAsync(voucherTypeId, ct);

        var entry = JournalEntry.Create(
            date: date, fyId: fyId, periodId: periodId,
            source: JournalEntrySource.System, description: description,
            refType: referenceType, refId: null, refNumber: referenceNumber,
            type: JournalEntryType.Normal, currency: currency,
            entryNumber: nextNum.ToString(),
            voucherTypeId: voucherTypeId, voucherSequence: voucherSeq);

        foreach (var l in lines)
        {
            if (l.IsDebit) entry.AddDebit(l.AccountId, l.Amount, l.Desc);
            else entry.AddCredit(l.AccountId, l.Amount, l.Desc);
        }

        const string VoucherPostPermission = "Accounting.Vouchers.Post";
        var canPost = _currentUser.IsSuperAdmin || _currentUser.HasPermission(VoucherPostPermission);
        if (postImmediately && canPost)
            entry.Post(_currentUser.UserId?.ToString() ?? "system");

        await _db.JournalEntries.AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
        return entry;
    }
}

// ─────────────────────────────────────────────────────────────────────
// Command: تعديل مناقلة بانتظار الاستلام
// يُعيد توليد قيد الإرسال بالقيم الجديدة (المبلغ/التاريخ/الحساب الوسيط)،
// ويُلغي القيد القديم: إن كان مرحَّلاً يُعكَس بقيد عكس مع الأثر التدقيقي،
// وإن كان مسوَّدة يُحذف soft-delete. الصناديق والعملة لا تُعدَّل هنا.
// ─────────────────────────────────────────────────────────────────────

public record UpdateCashBoxTransferCommand(int TransferId, UpdateCashBoxTransferDto Data)
    : IRequest<Result<int>>;

public class UpdateCashBoxTransferHandler : IRequestHandler<UpdateCashBoxTransferCommand, Result<int>>
{
    private readonly IAccountingDbContext _db;
    private readonly IPeriodResolver _periods;
    private readonly ICurrentUserService _currentUser;

    public UpdateCashBoxTransferHandler(
        IAccountingDbContext db,
        IPeriodResolver periods,
        ICurrentUserService currentUser)
    {
        _db = db;
        _periods = periods;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(UpdateCashBoxTransferCommand req, CancellationToken ct)
    {
        try
        {
            var d = req.Data;
            if (d.Amount <= 0) return Result.Failure<int>("مبلغ المناقلة يجب أن يكون أكبر من صفر.");
            if (d.TransitAccountId <= 0) return Result.Failure<int>("الحساب الوسيط مطلوب.");

            var transfer = await _db.CashBoxTransfers
                .Include(t => t.FromCashBox)
                    .ThenInclude(b => b!.Account)
                .Include(t => t.ToCashBox)
                .Include(t => t.SendJournalEntry)
                    .ThenInclude(e => e!.Lines)
                .FirstOrDefaultAsync(t => t.Id == req.TransferId, ct);
            if (transfer == null) return Result.Failure<int>("المناقلة غير موجودة.");
            if (transfer.Status != CashBoxTransferStatus.PendingReceive)
                return Result.Failure<int>(
                    $"لا يمكن تعديل مناقلة حالتها '{transfer.Status}' — التعديل متاح فقط 'بانتظار الاستلام'.");
            if (transfer.SendJournalEntry == null)
                return Result.Failure<int>("قيد الإرسال غير موجود لهذه المناقلة.");

            var fromBox = transfer.FromCashBox!;
            var toBox = transfer.ToCashBox!;
            var cur = transfer.Currency;

            // ‎التحقق من الحساب الوسيط الجديد
            var transitAcc = await _db.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == d.TransitAccountId, ct);
            if (transitAcc == null) return Result.Failure<int>("الحساب الوسيط غير موجود.");
            if (!transitAcc.IsActive) return Result.Failure<int>($"الحساب الوسيط '{transitAcc.NameAr}' معطّل.");
            if (!transitAcc.IsLeaf)
                return Result.Failure<int>($"الحساب الوسيط '{transitAcc.NameAr}' حساب رئيسي — اختر حساباً فرعياً.");
            var isTransitLinked = await _db.CashBoxes.AsNoTracking()
                .AnyAsync(b => b.AccountId == d.TransitAccountId, ct);
            if (isTransitLinked)
                return Result.Failure<int>(
                    $"الحساب الوسيط '{transitAcc.NameAr}' مرتبط بصندوق آخر — استخدم حساباً وسيطاً مستقلاً.");

            var activeFy = await _db.FiscalYears.AsNoTracking()
                .FirstOrDefaultAsync(f => f.IsActive, ct);
            if (activeFy != null
                && (d.SendDate.Date < activeFy.StartDate.Date || d.SendDate.Date > activeFy.EndDate.Date))
            {
                return Result.Failure<int>(
                    $"تاريخ الإرسال ({d.SendDate:yyyy-MM-dd}) خارج السنة المالية النشطة '{activeFy.Name}'.");
            }

            await using var tx = await _db.BeginTransactionAsync(ct);

            var ctOut = await EnsureSystemVoucherTypeAsync("CT-OUT", "تحويل صادر", "Cash Transfer Out",
                VoucherNature.Credit, displayOrder: 200, ct);

            // ‎(1) فحص سقف الصندوق المُرسِل بالمبلغ الجديد، مستثنياً قيد الإرسال
            //     القديم لأنه على وشك الإلغاء/الحذف.
            var fromCheck = await CashBoxGuard.ValidateAsync(_db,
                new[] {
                    new CashBoxGuard.LineSnapshot(fromBox.AccountId, IsDebit: false, d.Amount),
                    new CashBoxGuard.LineSnapshot(d.TransitAccountId, IsDebit: true, d.Amount),
                },
                cur, ctOut.Id, excludeJournalEntryId: transfer.SendJournalEntryId, ct);
            if (fromCheck != null) return Result.Failure<int>(fromCheck);

            var sendCurCheck = await EnsureCurrencyHasActiveBulletin(cur, d.SendDate, ct);
            if (sendCurCheck != null) return Result.Failure<int>(sendCurCheck);

            // ‎(2) إلغاء قيد الإرسال القديم
            var oldEntry = transfer.SendJournalEntry;
            if (oldEntry.Status == JournalEntryStatus.Posted)
            {
                var nextRevNum = await _db.GetNextJournalEntryNumberAsync(oldEntry.FiscalYearId, ct);
                var rev = oldEntry.CreateReversal(
                    $"تعديل مناقلة {transfer.TransferNumber}", nextRevNum.ToString());
                await _db.JournalEntries.AddAsync(rev, ct);
                if (d.PostImmediately)
                {
                    const string VoucherPostPermission = "Accounting.Vouchers.Post";
                    var canPost = _currentUser.IsSuperAdmin || _currentUser.HasPermission(VoucherPostPermission);
                    if (canPost) rev.Post(_currentUser.UserId?.ToString() ?? "system");
                }
                oldEntry.MarkAsReversed(rev.Id);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                oldEntry.MarkAsDeleted();
                foreach (var line in oldEntry.Lines) line.MarkAsDeleted();
                await _db.SaveChangesAsync(ct);
            }

            // ‎(3) قيد الإرسال الجديد
            var refLabel = $"مناقلة {transfer.TransferNumber}: {fromBox.NameAr} ⇒ {toBox.NameAr}";
            var newSendEntry = await BuildAndAddJournalEntryAsync(
                date: d.SendDate,
                description: $"{refLabel} — إرسال (تعديل)" +
                    (string.IsNullOrWhiteSpace(d.Description) ? "" : $" ({d.Description})"),
                voucherTypeId: ctOut.Id,
                currency: cur,
                lines: new (int AccountId, bool IsDebit, decimal Amount, string? Desc)[]
                {
                    (d.TransitAccountId, true,  d.Amount, $"تحت التحويل إلى {toBox.NameAr}"),
                    (fromBox.AccountId,  false, d.Amount, $"إخراج من {fromBox.NameAr}"),
                },
                referenceType: "CashBoxTransfer",
                referenceNumber: transfer.TransferNumber,
                postImmediately: d.PostImmediately,
                ct: ct);

            // ‎(4) تحديث سجل المناقلة
            transfer.UpdatePending(
                newSendEntry.Id, d.Amount, d.SendDate,
                d.TransitAccountId, d.Description, d.ReferenceNumber);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success(newSendEntry.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }

    private async Task<JournalVoucherType> EnsureSystemVoucherTypeAsync(
        string code, string nameAr, string? nameEn, VoucherNature nature, int displayOrder, CancellationToken ct)
    {
        var existing = await _db.JournalVoucherTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Code == code, ct);
        if (existing != null)
        {
            if (existing.IsDeleted) { existing.Restore(); if (!existing.IsEnabled) existing.SetEnabled(true); }
            return existing;
        }
        var vt = JournalVoucherType.Create(
            code: code, nameAr: nameAr, nameEn: nameEn,
            description: "نوع سند نظامي للمناقلات بين الصناديق",
            isEnabled: true, isSystem: true, displayOrder: displayOrder,
            nature: nature, showInSidebar: false);
        await _db.JournalVoucherTypes.AddAsync(vt, ct);
        await _db.SaveChangesAsync(ct);
        return vt;
    }

    private async Task<JournalEntry> BuildAndAddJournalEntryAsync(
        DateTime date, string description, int voucherTypeId, string currency,
        IReadOnlyList<(int AccountId, bool IsDebit, decimal Amount, string? Desc)> lines,
        string? referenceType, string? referenceNumber, bool postImmediately, CancellationToken ct)
    {
        var (fyId, periodId) = await _periods.ResolveAsync(date, ct);
        var nextNum = await _db.GetNextJournalEntryNumberAsync(fyId, ct);
        var voucherSeq = await _db.GetNextVoucherSequenceAsync(voucherTypeId, ct);

        var entry = JournalEntry.Create(
            date: date, fyId: fyId, periodId: periodId,
            source: JournalEntrySource.System, description: description,
            refType: referenceType, refId: null, refNumber: referenceNumber,
            type: JournalEntryType.Normal, currency: currency,
            entryNumber: nextNum.ToString(),
            voucherTypeId: voucherTypeId, voucherSequence: voucherSeq);

        foreach (var l in lines)
        {
            if (l.IsDebit) entry.AddDebit(l.AccountId, l.Amount, l.Desc);
            else entry.AddCredit(l.AccountId, l.Amount, l.Desc);
        }

        const string VoucherPostPermission = "Accounting.Vouchers.Post";
        var canPost = _currentUser.IsSuperAdmin || _currentUser.HasPermission(VoucherPostPermission);
        if (postImmediately && canPost)
            entry.Post(_currentUser.UserId?.ToString() ?? "system");

        await _db.JournalEntries.AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    private async Task<string?> EnsureCurrencyHasActiveBulletin(string currency, DateTime entryDate, CancellationToken ct)
    {
        var cur = (currency ?? "IQD").Trim().ToUpperInvariant();
        var atUtc = DateTime.SpecifyKind(entryDate, DateTimeKind.Unspecified)
            .Date.AddDays(1).AddTicks(-1);

        var bulletin = await _db.CurrencyRateBulletins
            .Include(b => b.Lines)
            .Where(b => b.Status == CurrencyRateBulletinStatus.Published && b.EffectiveAt <= atUtc)
            .OrderByDescending(b => b.EffectiveAt).ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

        if (bulletin != null && string.Equals(bulletin.BaseCurrency, cur, StringComparison.OrdinalIgnoreCase))
            return null;
        if (bulletin == null)
        {
            if (cur == "IQD") return null;
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار — لا توجد نشرة منشورة سارية بتاريخ {entryDate:yyyy-MM-dd}.";
        }
        var hasLine = bulletin.Lines.Any(l => string.Equals(l.Currency, cur, StringComparison.OrdinalIgnoreCase));
        if (!hasLine)
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار '{bulletin.Name}' — أضف سعر صرف لها أو أصدر نشرة جديدة.";
        return null;
    }
}

// ─────────────────────────────────────────────────────────────────────
// Command: حذف مناقلة ملغاة نهائياً (مع جميع قيودها المحاسبية)
// متاح فقط للمناقلات في حالة Cancelled. السبب: قيود الإلغاء (الـ
// Reversal) هي العكس الحسابي لقيد الإرسال، فحذف الزوج معاً يُبقي على
// تكامل دفتر الأستاذ (المجموع = 0). أمّا المناقلات في PendingReceive
// أو Received فيجب إلغاؤها أوّلاً عبر مسار Cancel/Unreceive ثم حذفها.
// نلتقط أيضاً أي قيود أخرى تشترك معها بـ ReferenceNumber (مثل قيد
// الاستلام السابق وعكسه إن مرّت المناقلة بـ Received → Unreceive →
// Cancel) كي لا تترك سجلات يتيمة في دفتر الأستاذ.
// ─────────────────────────────────────────────────────────────────────

public record DeleteCashBoxTransferCommand(int TransferId, DeleteCashBoxTransferDto Data)
    : IRequest<Result<bool>>;

public class DeleteCashBoxTransferHandler : IRequestHandler<DeleteCashBoxTransferCommand, Result<bool>>
{
    private readonly IAccountingDbContext _db;

    public DeleteCashBoxTransferHandler(IAccountingDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteCashBoxTransferCommand req, CancellationToken ct)
    {
        try
        {
            var transfer = await _db.CashBoxTransfers
                .FirstOrDefaultAsync(t => t.Id == req.TransferId, ct);
            if (transfer == null) return Result.Failure<bool>("المناقلة غير موجودة.");

            if (transfer.Status != CashBoxTransferStatus.Cancelled)
                return Result.Failure<bool>(
                    $"لا يمكن حذف مناقلة حالتها '{transfer.Status}'. " +
                    "يجب إلغاء المناقلة أولاً (تراجع عن الاستلام إن لزم) ثم حذفها.");

            // ‎جميع القيود المرتبطة بهذه المناقلة (إرسال + عكس الإلغاء + أي
            // ‎قيود استلام/عكس استلام تاريخية إن مرّت بدورة Received → Unreceive
            // ‎قبل الإلغاء النهائي). نلتقطها بـ ReferenceNumber لأنه ثابت لكل
            // ‎قيود المناقلة.
            var relatedEntries = await _db.JournalEntries
                .Include(e => e.Lines)
                .Where(e => e.ReferenceNumber == transfer.TransferNumber
                    && (e.ReferenceType == "CashBoxTransfer"
                        || e.ReferenceType == "CashBoxTransferReversal"))
                .ToListAsync(ct);

            await using var tx = await _db.BeginTransactionAsync(ct);

            transfer.MarkAsDeleted();

            foreach (var e in relatedEntries)
            {
                e.MarkAsDeleted();
                foreach (var l in e.Lines) l.MarkAsDeleted();
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success(true);
        }
        catch (DomainException ex) { return Result.Failure<bool>(ex.Message); }
    }
}
