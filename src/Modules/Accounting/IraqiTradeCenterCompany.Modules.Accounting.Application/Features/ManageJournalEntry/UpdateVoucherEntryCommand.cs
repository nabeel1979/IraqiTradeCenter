using IraqiTradeCenterCompany.Modules.Accounting.Application.Internal;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageJournalEntry;

/// <summary>
/// تحديث قيد مولَّد من سند مخصّص (سند قبض/سند دفع/…).
///   - يطلب أن يكون القيد فعلاً لديه VoucherTypeId.
///   - يستقبل بنوداً جديدة (سطرين فقط في معظم الحالات: الصندوق + الحساب المقابل).
///   - يكرر نفس قواعد التحقّق المستخدمة في UpdateJournalEntry (العملة، الحسابات، التوازن).
///
/// نقطة النهاية: PUT /api/accounting/vouchers/{id}
/// </summary>
public record UpdateVoucherEntryCommand(
    int Id,
    DateTime EntryDate,
    string Description,
    string Currency,
    List<UpdateJournalLine> Lines,
    bool PostImmediately = true,
    /// <summary>الرقم اليدوي للسند (شيك، إيصال خارجي، …).</summary>
    string? ManualNumber = null
) : IRequest<Result<int>>;

public class UpdateVoucherEntryHandler : IRequestHandler<UpdateVoucherEntryCommand, Result<int>>
{
    private readonly IAccountingDbContext _db;
    private readonly IraqiTradeCenterCompany.SharedKernel.Interfaces.ICurrentUserService _currentUser;
    private readonly IAuditLogger _audit;

    public UpdateVoucherEntryHandler(IAccountingDbContext db,
        IraqiTradeCenterCompany.SharedKernel.Interfaces.ICurrentUserService currentUser,
        IAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Result<int>> Handle(UpdateVoucherEntryCommand req, CancellationToken ct)
    {
        try
        {
            var entry = await _db.JournalEntries.Include(e => e.Lines)
                .FirstOrDefaultAsync(e => e.Id == req.Id, ct);
            if (entry == null) return Result.Failure<int>("القيد غير موجود");
            if (entry.Status == JournalEntryStatus.Reversed)
                return Result.Failure<int>("لا يمكن تعديل قيد معكوس");

            // ‎قفل قيود المناقلات بين الصناديق
            if (entry.ReferenceType == "CashBoxTransfer" || entry.ReferenceType == "CashBoxTransferReversal")
                return Result.Failure<int>(
                    "هذا القيد مولَّد من مناقلة بين صندوقَين — لا يُعدَّل من نافذة السندات. " +
                    "افتح صفحة الصناديق ⇒ تبويب 'المناقلات' وقم بالتراجع عن الاستلام أو الإلغاء أولاً.");

            if (!entry.VoucherTypeId.HasValue)
                return Result.Failure<int>("هذا القيد ليس مولَّداً من سند — استخدم تعديل القيد العادي");

            // ‎حارس السنة المالية النشطة:
            //   التاريخ الأصلي للسند (المخزَّن) والتاريخ الجديد المُرسَل من
            //   الواجهة كلاهما يجب أن يقعا ضمن نطاق السنة المالية المُفَعَّلة.
            //   لا يستطيع المستخدم الالتفاف على القيد بتغيير حقل التاريخ.
            var activeFy = await _db.FiscalYears.AsNoTracking()
                .FirstOrDefaultAsync(f => f.IsActive, ct);
            if (activeFy != null)
            {
                var originalDate = entry.EntryDate.Date;
                if (originalDate < activeFy.StartDate.Date || originalDate > activeFy.EndDate.Date)
                {
                    return Result.Failure<int>(
                        $"تاريخ هذا السند ({originalDate:yyyy-MM-dd}) خارج السنة المالية النشطة '{activeFy.Name}'. لتعديله، فعِّل السنة المالية المناسبة أولاً.");
                }
                var newDate = req.EntryDate.Date;
                if (newDate < activeFy.StartDate.Date || newDate > activeFy.EndDate.Date)
                {
                    return Result.Failure<int>(
                        $"التاريخ الجديد ({newDate:yyyy-MM-dd}) خارج السنة المالية النشطة '{activeFy.Name}'.");
                }
            }

            if (req.Lines == null || req.Lines.Count < 2)
                return Result.Failure<int>("السند لازم سطرين على الأقل");

            var d = req.Lines.Where(l => l.IsDebit).Sum(l => l.Amount);
            var c = req.Lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
            if (Math.Round(d, 3) != Math.Round(c, 3))
                return Result.Failure<int>("السند غير متوازن");

            var accountIds = req.Lines.Select(l => l.AccountId).Distinct().ToList();
            var accounts = await _db.Accounts
                .Where(a => accountIds.Contains(a.Id) && a.IsActive).ToListAsync(ct);
            if (accounts.Count != accountIds.Count)
                return Result.Failure<int>("بعض الحسابات غير موجودة أو غير مفعّلة");
            var nonLeaf = accounts.FirstOrDefault(a => !a.IsLeaf);
            if (nonLeaf != null) return Result.Failure<int>($"الحساب '{nonLeaf.NameAr}' حساب رئيسي - لا يقبل قيوداً");

            // التحقق من تسعير العملة
            var currencyCheck = await EnsureCurrencyHasActiveBulletin(req.Currency, req.EntryDate, ct);
            if (currencyCheck != null) return Result.Failure<int>(currencyCheck);

            // ‎فحص قواعد الصناديق (سقوف + منع استخدامها في قيد غير سند)
            var cashBoxCheck = await CashBoxGuard.ValidateAsync(
                _db,
                req.Lines.Select(l => new CashBoxGuard.LineSnapshot(l.AccountId, l.IsDebit, l.Amount)).ToList(),
                req.Currency,
                entry.VoucherTypeId,
                excludeJournalEntryId: entry.Id,
                ct);
            if (cashBoxCheck != null) return Result.Failure<int>(cashBoxCheck);

            // ‎نفك الترحيل قبل تعديل البنود ثم نُرحّل من جديد فقط إذا طلب المستخدم.
            // ‎بهذه الطريقة يستطيع المستخدم تحويل قيد مُرحَّل إلى مسودة عبر إلغاء
            // ‎علامة "ترحيل فوري" وحفظ السند.
            if (entry.Status == JournalEntryStatus.Posted) entry.Unpost();

            entry.UpdateBasic(req.EntryDate, req.Description, entry.EntryType, req.Currency, entry.VoucherTypeId, req.ManualNumber);
            entry.ReplaceLines(req.Lines.Select(l =>
                (l.AccountId, l.IsDebit, l.Amount, l.Description)).ToList());

            if (req.PostImmediately)
                entry.Post(_currentUser.UserId?.ToString() ?? "system");

            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(
                entityType: "Voucher",
                entityId: entry.Id.ToString(),
                action: AuditActions.Update,
                summary: $"تعديل سند رقم {entry.VoucherSequence ?? 0} — {entry.Description}",
                details: new
                {
                    entry.EntryNumber,
                    entry.VoucherTypeId,
                    entry.VoucherSequence,
                    entry.ManualNumber,
                    entry.TotalDebit,
                    entry.TotalCredit,
                    entry.Currency,
                    status = entry.Status.ToString(),
                },
                ct: ct);

            return Result.Success(entry.Id);
        }
        catch (UnbalancedJournalEntryException ex) { return Result.Failure<int>(ex.Message); }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
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
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار '{bulletin.Name}'.";

        return null;
    }
}

/// <summary>أمر حذف قيد سند مخصّص (يتجاوز قيد منع حذف القيود المُدارة في DeleteJournalEntryCommand).</summary>
public record DeleteVoucherEntryCommand(int Id) : IRequest<Result<bool>>;

public class DeleteVoucherEntryHandler : IRequestHandler<DeleteVoucherEntryCommand, Result<bool>>
{
    private readonly IAccountingDbContext _db;
    private readonly IAuditLogger _audit;
    public DeleteVoucherEntryHandler(IAccountingDbContext db, IAuditLogger audit)
    { _db = db; _audit = audit; }

    public async Task<Result<bool>> Handle(DeleteVoucherEntryCommand req, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == req.Id, ct);
        if (entry == null) return Result.Failure<bool>("القيد غير موجود");
        if (entry.Status == JournalEntryStatus.Reversed)
            return Result.Failure<bool>("لا يمكن حذف قيد معكوس");
        if (!entry.VoucherTypeId.HasValue)
            return Result.Failure<bool>("هذا القيد ليس سنداً — استخدم حذف القيد العادي");

        entry.MarkAsDeleted();
        foreach (var line in entry.Lines) line.MarkAsDeleted();

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            entityType: "Voucher",
            entityId: entry.Id.ToString(),
            action: AuditActions.Delete,
            summary: $"حذف سند رقم {entry.VoucherSequence ?? 0} — {entry.Description}",
            details: new { entry.EntryNumber, entry.VoucherTypeId, entry.VoucherSequence, entry.TotalDebit, entry.TotalCredit },
            ct: ct);

        return Result.Success(true);
    }
}
