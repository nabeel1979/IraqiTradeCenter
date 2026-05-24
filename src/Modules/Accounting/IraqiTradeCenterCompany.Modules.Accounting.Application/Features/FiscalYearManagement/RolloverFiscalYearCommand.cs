using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;

/// <summary>
/// أنماط تدوير الأرصدة الثلاثة:
///   • <c>WithProfitLoss</c>: يحسب صافي الربح/الخسارة من Revenue-Expense ويرحّله
///     لحساب الأرباح أو الخسارة. يُدوّر الميزانية (Asset/Liability/Equity).
///   • <c>BalanceSheetOnly</c>: يدوّر أرصدة الميزانية فقط (Asset/Liability/Equity)
///     كما هي بدون احتساب ربح/خسارة. مفيد للتدوير الجزئي.
///   • <c>AllAccounts</c>: يدوّر كل الحسابات بأرصدتها (الميزانية + الإيرادات +
///     المصروفات). يُستخدم عند الحاجة للحفاظ على أرصدة Revenue/Expense كأرصدة
///     افتتاحية في السنة الجديدة (سيناريو خاص).
/// </summary>
public enum RolloverMode
{
    WithProfitLoss = 1,
    BalanceSheetOnly = 2,
    AllAccounts = 3,
}

/// <summary>
/// أمر تدوير الأرصدة من سنة مالية مغلقة إلى سنة مالية لاحقة.
/// شروط:
///   • السنة المصدر يجب أن تكون مغلقة (يضمن ثبات الأرصدة).
///   • السنة الهدف يجب أن تكون لاحقة (StartDate &gt; EndDate المصدر).
///   • السنة الهدف يجب ألا تحوي قيداً افتتاحياً سابقاً.
/// </summary>
public record RolloverFiscalYearCommand(
    int SourceFiscalYearId,
    int TargetFiscalYearId,
    string PerformedBy,
    string? ProfitAccountCode,
    string? LossAccountCode,
    RolloverMode Mode = RolloverMode.WithProfitLoss,
    bool PreviewOnly = false,
    DateTime? OpeningEntryDate = null
) : IRequest<FiscalYearRolloverResultDto>;

public class RolloverFiscalYearHandler : IRequestHandler<RolloverFiscalYearCommand, FiscalYearRolloverResultDto>
{
    private readonly IAccountingDbContext _db;
    public RolloverFiscalYearHandler(IAccountingDbContext db) => _db = db;

    public async Task<FiscalYearRolloverResultDto> Handle(RolloverFiscalYearCommand req, CancellationToken ct)
    {
        // ─── 1) تحقّق السنوات ────────────────────────────────────────────────
        var src = await _db.FiscalYears
            .FirstOrDefaultAsync(f => f.Id == req.SourceFiscalYearId, ct)
            ?? throw new DomainException("السنة المصدر غير موجودة");
        var dst = await _db.FiscalYears
            .Include(f => f.Periods)
            .FirstOrDefaultAsync(f => f.Id == req.TargetFiscalYearId, ct)
            ?? throw new DomainException("السنة الهدف غير موجودة");

        if (!src.IsClosed)
            throw new DomainException(
                "لا يمكن تدوير الأرصدة من سنة مفتوحة. يجب إغلاق السنة المصدر أولاً " +
                "لضمان ثبات أرصدتها قبل التدوير.");

        if (dst.IsClosed)
            throw new DomainException("لا يمكن التدوير إلى سنة مالية مغلقة");

        if (dst.StartDate <= src.EndDate)
            throw new DomainException("السنة الهدف يجب أن تكون بعد السنة المصدر زمنياً");

        // ‎فحص وجود قيد افتتاحي سابق في السنة الهدف.
        var existingOpening = await _db.JournalEntries.AsNoTracking()
            .AnyAsync(e => e.FiscalYearId == dst.Id && e.EntryType == JournalEntryType.Opening, ct);
        if (existingOpening && !req.PreviewOnly)
            throw new DomainException(
                "يوجد قيد افتتاحي سابق في السنة الهدف. احذفه (تراجع عن التدوير) قبل المحاولة مجدداً.");

        // ─── 2) تحديد تاريخ القيد الافتتاحي والفترة المناسبة ──────────────────
        var openingDate = (req.OpeningEntryDate?.Date) ?? dst.StartDate.Date;
        if (openingDate < dst.StartDate.Date || openingDate > dst.EndDate.Date)
            throw new DomainException("تاريخ القيد الافتتاحي يجب أن يقع داخل السنة الهدف");

        var firstPeriod = dst.Periods.OrderBy(p => p.StartDate)
            .FirstOrDefault(p => p.StartDate.Date <= openingDate && p.EndDate.Date >= openingDate)
            ?? throw new DomainException("لا توجد فترة محاسبية تغطي تاريخ القيد الافتتاحي في السنة الهدف");

        // ─── 3) جمع أرصدة الحسابات الورقية (Leaf) من السنة المصدر ────────────
        // ‎الرصيد = OpeningBalance + Σ(Posted Debit - Posted Credit) خلال السنة المصدر.
        // ‎نستخدم join يدوي لأن JournalEntryLine ليس لديه navigation إلى JournalEntry.
        var srcEntryAggregates = await (
            from l in _db.JournalEntryLines.AsNoTracking()
            join e in _db.JournalEntries.AsNoTracking() on l.JournalEntryId equals e.Id
            where e.FiscalYearId == src.Id && e.Status == JournalEntryStatus.Posted
            group l by l.AccountId into g
            select new
            {
                AccountId = g.Key,
                Debit = g.Where(x => x.IsDebit).Sum(x => (decimal?)x.Amount) ?? 0m,
                Credit = g.Where(x => !x.IsDebit).Sum(x => (decimal?)x.Amount) ?? 0m,
            }).ToListAsync(ct);

        var aggDict = srcEntryAggregates.ToDictionary(a => a.AccountId);

        var allAccounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.IsActive && a.IsLeaf)
            .ToListAsync(ct);

        // ‎صافي الرصيد لكل حساب (Debit - Credit) — موجب يعني رصيد مدين.
        var balances = new List<(Account Account, decimal Net)>();
        foreach (var acc in allAccounts)
        {
            var openingNet = acc.Nature == AccountNature.Debit
                ? acc.OpeningBalance
                : -acc.OpeningBalance;
            var movement = aggDict.TryGetValue(acc.Id, out var a)
                ? a.Debit - a.Credit
                : 0m;
            var net = openingNet + movement;
            if (Math.Round(net, 3) == 0m) continue;
            balances.Add((acc, net));
        }

        // ─── 4) فصل أرصدة الحسابات حسب النوع ─────────────────────────────────
        var bsBalances = balances
            .Where(b => b.Account.Type == AccountType.Asset
                     || b.Account.Type == AccountType.Liability
                     || b.Account.Type == AccountType.Equity)
            .ToList();

        var pnlBalances = balances
            .Where(b => b.Account.Type == AccountType.Revenue
                     || b.Account.Type == AccountType.Expense)
            .ToList();

        // ‎صافي الربح/الخسارة = إجمالي الإيرادات - إجمالي المصروفات.
        decimal totalRevenue = -pnlBalances.Where(b => b.Account.Type == AccountType.Revenue).Sum(b => b.Net);
        decimal totalExpense = pnlBalances.Where(b => b.Account.Type == AccountType.Expense).Sum(b => b.Net);
        decimal netProfit = totalRevenue - totalExpense; // ‎موجب=ربح، سالب=خسارة

        // ‎الحسابات التي ستظهر في القيد الافتتاحي حسب النمط:
        var rollingBalances = req.Mode switch
        {
            RolloverMode.AllAccounts => balances,                       // ‎كل الحسابات (4 أنواع)
            RolloverMode.WithProfitLoss => bsBalances,                  // ‎الميزانية فقط + سطر الربح/الخسارة
            RolloverMode.BalanceSheetOnly => bsBalances,                // ‎الميزانية فقط
            _ => bsBalances,
        };

        // ─── 5) في وضع المعاينة فقط — أرجع النتيجة بدون تعديل ─────────────────
        if (req.PreviewOnly)
        {
            var msg = req.Mode switch
            {
                RolloverMode.WithProfitLoss =>
                    $"معاينة: سيتم تدوير {bsBalances.Count} حساب ميزانية + ترحيل صافي {(netProfit >= 0 ? "ربح" : "خسارة")} = {Math.Abs(netProfit):N3}",
                RolloverMode.AllAccounts =>
                    $"معاينة: سيتم تدوير {balances.Count} حساب (ميزانية + إيرادات + مصاريف) كرصيد افتتاحي",
                _ =>
                    $"معاينة: سيتم تدوير {bsBalances.Count} حساب ميزانية بدون احتساب ربح/خسارة",
            };
            return new FiscalYearRolloverResultDto
            {
                Success = true,
                FromFiscalYearId = src.Id,
                ToFiscalYearId = dst.Id,
                BalanceSheetAccountsRolled = rollingBalances.Count,
                RetainedEarningsTransferred = req.Mode == RolloverMode.WithProfitLoss ? netProfit : 0m,
                Message = msg,
            };
        }

        // ─── 6) تنفيذ التدوير ضمن معاملة ─────────────────────────────────────
        await using var trx = await _db.BeginTransactionAsync(ct);

        // ‎جلب حسابات الربح/الخسارة (للوضع WithProfitLoss فقط).
        Account? profitAcc = null, lossAcc = null;
        if (req.Mode == RolloverMode.WithProfitLoss)
        {
            if (string.IsNullOrWhiteSpace(req.ProfitAccountCode))
                throw new DomainException("كود حساب الأرباح مطلوب في وضع 'إقفال مع الربح/الخسارة'");
            if (string.IsNullOrWhiteSpace(req.LossAccountCode))
                throw new DomainException("كود حساب الخسائر مطلوب في وضع 'إقفال مع الربح/الخسارة'");

            profitAcc = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Code == req.ProfitAccountCode && a.IsLeaf && a.IsActive, ct)
                ?? throw new DomainException($"حساب الأرباح بالكود {req.ProfitAccountCode} غير موجود أو غير نشط أو ليس ورقياً");
            lossAcc = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Code == req.LossAccountCode && a.IsLeaf && a.IsActive, ct)
                ?? throw new DomainException($"حساب الخسائر بالكود {req.LossAccountCode} غير موجود أو غير نشط أو ليس ورقياً");
        }

        // ‎بناء سطور القيد الافتتاحي.
        var lines = new List<(int AccountId, bool IsDebit, decimal Amount, string? Description)>();
        decimal totalDebit = 0m, totalCredit = 0m;
        foreach (var (acc, net) in rollingBalances)
        {
            if (net > 0m)
            {
                lines.Add((acc.Id, true, net, "رصيد افتتاحي مُدوَّر"));
                totalDebit += net;
            }
            else
            {
                lines.Add((acc.Id, false, -net, "رصيد افتتاحي مُدوَّر"));
                totalCredit += -net;
            }
        }

        // ‎في وضع With Profit/Loss: نُضيف سطر الربح/الخسارة ليُعدّل التوازن.
        if (req.Mode == RolloverMode.WithProfitLoss && Math.Round(netProfit, 3) != 0m)
        {
            if (netProfit > 0m)
            {
                lines.Add((profitAcc!.Id, false, netProfit, "صافي الربح المرحَّل من السنة السابقة"));
                totalCredit += netProfit;
            }
            else
            {
                lines.Add((lossAcc!.Id, true, -netProfit, "صافي الخسارة المرحَّلة من السنة السابقة"));
                totalDebit += -netProfit;
            }
        }

        // ‎موازنة احتمالات الـ rounding (في الميزان الأصلي هناك توازن أصلاً، لكن
        // ‎عمليات Math.Round قد تنتج فروق مليمات). نرفض القيد إن كان غير متوازن.
        if (Math.Round(totalDebit - totalCredit, 3) != 0m)
            throw new DomainException(
                $"القيد الافتتاحي غير متوازن. مدين={totalDebit:N3}، دائن={totalCredit:N3}، فرق={(totalDebit - totalCredit):N3}. " +
                "تحقّق من اكتمال إغلاق السنة السابقة.");

        if (lines.Count == 0)
            throw new DomainException("لا توجد أرصدة ميزانية للتدوير");

        // ‎إنشاء القيد الافتتاحي.
        var modeLabel = req.Mode switch
        {
            RolloverMode.WithProfitLoss => "مع إقفال الأرباح/الخسائر",
            RolloverMode.AllAccounts => "ترحيل كامل (شامل الإيرادات والمصاريف)",
            _ => "بدون تغيير",
        };

        var entryNum = await _db.GetNextJournalEntryNumberAsync(dst.Id, ct);
        var entry = JournalEntry.Create(
            date: openingDate,
            fyId: dst.Id,
            periodId: firstPeriod.Id,
            source: JournalEntrySource.Manual,
            description: $"قيد افتتاحي مُدوَّر من {src.Name} ({modeLabel})",
            type: JournalEntryType.Opening,
            currency: "IQD",
            entryNumber: entryNum.ToString());

        entry.ReplaceLines(lines);
        entry.Post(req.PerformedBy ?? "system");
        await _db.JournalEntries.AddAsync(entry, ct);

        // ‎تحديث OpeningBalance لكل حساب مُدوَّر (الميزانية + الإيرادات/المصاريف
        // ‎في وضع AllAccounts).
        var rolledAccountIds = rollingBalances.Select(b => b.Account.Id).ToList();
        var trackedAccounts = await _db.Accounts
            .Where(a => rolledAccountIds.Contains(a.Id))
            .ToListAsync(ct);
        foreach (var ta in trackedAccounts)
        {
            var net = rollingBalances.First(b => b.Account.Id == ta.Id).Net;
            var stored = ta.Nature == AccountNature.Debit ? net : -net;
            ta.SetOpeningBalance(stored);
        }

        await _db.SaveChangesAsync(ct);
        await trx.CommitAsync(ct);

        var resultMsg = req.Mode switch
        {
            RolloverMode.WithProfitLoss =>
                $"تم تدوير {bsBalances.Count} حساب ميزانية وترحيل صافي {(netProfit >= 0 ? "ربح" : "خسارة")} = {Math.Abs(netProfit):N3}",
            RolloverMode.AllAccounts =>
                $"تم تدوير {rollingBalances.Count} حساب (ميزانية + إيرادات + مصاريف) كرصيد افتتاحي",
            _ =>
                $"تم تدوير {bsBalances.Count} حساب ميزانية بدون احتساب ربح/خسارة",
        };

        return new FiscalYearRolloverResultDto
        {
            Success = true,
            FromFiscalYearId = src.Id,
            ToFiscalYearId = dst.Id,
            BalanceSheetAccountsRolled = rollingBalances.Count,
            RetainedEarningsTransferred = req.Mode == RolloverMode.WithProfitLoss ? netProfit : 0m,
            Message = resultMsg,
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────
// أمر التراجع عن التدوير: يحذف القيد الافتتاحي في السنة الهدف، يُعيد ضبط
// OpeningBalance للحسابات المتأثّرة، ويفك إغلاق السنة المصدر تلقائياً
// (اختيارياً) ليتمكّن المستخدم من تعديل قيود السنة السابقة.
// ─────────────────────────────────────────────────────────────────────────
public record UndoRolloverCommand(
    int TargetFiscalYearId,
    bool ReopenSource = true
) : IRequest<UndoRolloverResultDto>;

public class UndoRolloverResultDto
{
    public bool Success { get; set; }
    public int DeletedEntryId { get; set; }
    public int AffectedAccounts { get; set; }
    public int? ReopenedSourceId { get; set; }
    public string Message { get; set; } = default!;
}

public class UndoRolloverHandler : IRequestHandler<UndoRolloverCommand, UndoRolloverResultDto>
{
    private readonly IAccountingDbContext _db;
    public UndoRolloverHandler(IAccountingDbContext db) => _db = db;

    public async Task<UndoRolloverResultDto> Handle(UndoRolloverCommand req, CancellationToken ct)
    {
        var dst = await _db.FiscalYears
            .FirstOrDefaultAsync(f => f.Id == req.TargetFiscalYearId, ct)
            ?? throw new DomainException("السنة الهدف غير موجودة");

        if (dst.IsClosed)
            throw new DomainException("لا يمكن التراجع عن التدوير بعد إغلاق السنة الهدف. افكّ إغلاقها أولاً.");

        var opening = await _db.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.FiscalYearId == dst.Id && e.EntryType == JournalEntryType.Opening, ct)
            ?? throw new DomainException("لا يوجد قيد افتتاحي في السنة الهدف لإلغائه");

        // ‎فحص: هل توجد قيود لاحقة في السنة الهدف بناءً على هذه الأرصدة؟
        // ‎نسمح بالتراجع لكن نُنبّه المستخدم في الواجهة. هنا نحذف القيد الافتتاحي
        // ‎ونُعيد العنوان OpeningBalance للحسابات إلى الصفر.
        var affectedAccountIds = opening.Lines.Select(l => l.AccountId).Distinct().ToList();

        await using var trx = await _db.BeginTransactionAsync(ct);

        // ‎حذف القيد الافتتاحي وكل أسطره.
        _db.JournalEntryLines.RemoveRange(opening.Lines);
        _db.JournalEntries.Remove(opening);

        // ‎تصفير OpeningBalance للحسابات المتأثّرة (يُمكن للمستخدم لاحقاً
        // ‎ضبطها يدوياً أو تنفيذ تدوير جديد).
        var accs = await _db.Accounts
            .Where(a => affectedAccountIds.Contains(a.Id))
            .ToListAsync(ct);
        foreach (var a in accs) a.SetOpeningBalance(0m);

        await _db.SaveChangesAsync(ct);

        // ‎فك إغلاق السنة المصدر تلقائياً (إذا طُلب).
        int? reopenedId = null;
        if (req.ReopenSource)
        {
            // ‎السنة المصدر = السنة المالية السابقة (تنتهي قبل بداية السنة الهدف).
            var src = await _db.FiscalYears
                .Include(f => f.Periods)
                .Where(f => f.EndDate < dst.StartDate)
                .OrderByDescending(f => f.EndDate)
                .FirstOrDefaultAsync(ct);
            if (src != null && src.IsClosed)
            {
                src.Reopen();
                reopenedId = src.Id;
                await _db.SaveChangesAsync(ct);
            }
        }

        await trx.CommitAsync(ct);

        return new UndoRolloverResultDto
        {
            Success = true,
            DeletedEntryId = opening.Id,
            AffectedAccounts = affectedAccountIds.Count,
            ReopenedSourceId = reopenedId,
            Message = reopenedId.HasValue
                ? $"تم حذف القيد الافتتاحي وتصفير {affectedAccountIds.Count} حساب وفك إغلاق السنة السابقة"
                : $"تم حذف القيد الافتتاحي وتصفير {affectedAccountIds.Count} حساب",
        };
    }
}
