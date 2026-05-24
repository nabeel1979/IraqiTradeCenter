using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;

// ════════════════════════════════════════════════════════════════════════════
// أوامر إدارة الفترة المحاسبية الفردية: تعديل التواريخ، حذف، فتح/إغلاق/قفل.
// كلّها تتحقق من الشروط الآمنة قبل التنفيذ:
//   • التعديل/الحذف: لا قيود محاسبية تستخدم الفترة + السنة المالية ليست مغلقة.
//   • التعديل: التواريخ ضمن نطاق السنة المالية + لا تتداخل مع فترات أخرى.
//   • تغيير الحالة: السنة المالية ليست مغلقة (للحفاظ على تكامل الإغلاق).
// ════════════════════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────────────────
// 1) تعديل تواريخ فترة محاسبية
// ─────────────────────────────────────────────────────────────────────────
public record UpdateAccountingPeriodCommand(int PeriodId, DateTime StartDate, DateTime EndDate)
    : IRequest<Unit>;

public class UpdateAccountingPeriodHandler : IRequestHandler<UpdateAccountingPeriodCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public UpdateAccountingPeriodHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(UpdateAccountingPeriodCommand req, CancellationToken ct)
    {
        if (req.EndDate <= req.StartDate)
            throw new DomainException("تاريخ النهاية يجب أن يكون بعد البداية");

        var period = await _db.AccountingPeriods
            .FirstOrDefaultAsync(p => p.Id == req.PeriodId, ct)
            ?? throw new DomainException("الفترة غير موجودة");

        var fy = await _db.FiscalYears.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == period.FiscalYearId, ct)
            ?? throw new DomainException("السنة المالية غير موجودة");

        if (fy.IsClosed)
            throw new DomainException("لا يمكن تعديل فترة في سنة مالية مغلقة");

        // ‎التواريخ ضمن نطاق السنة
        if (req.StartDate.Date < fy.StartDate.Date || req.EndDate.Date > fy.EndDate.Date)
            throw new DomainException(
                $"تواريخ الفترة يجب أن تقع ضمن نطاق السنة المالية " +
                $"({fy.StartDate:yyyy-MM-dd} → {fy.EndDate:yyyy-MM-dd})");

        // ‎التداخل مع فترات أخرى في نفس السنة
        var siblings = await _db.AccountingPeriods.AsNoTracking()
            .Where(p => p.FiscalYearId == period.FiscalYearId && p.Id != period.Id)
            .Select(p => new { p.Id, p.StartDate, p.EndDate, p.PeriodNumber })
            .ToListAsync(ct);

        foreach (var s in siblings)
        {
            var intersects =
                (req.StartDate.Date >= s.StartDate.Date && req.StartDate.Date <= s.EndDate.Date) ||
                (req.EndDate.Date   >= s.StartDate.Date && req.EndDate.Date   <= s.EndDate.Date) ||
                (req.StartDate.Date <= s.StartDate.Date && req.EndDate.Date   >= s.EndDate.Date);
            if (intersects)
                throw new DomainException($"الفترة تتداخل مع الفترة رقم {s.PeriodNumber}");
        }

        // ‎إذا فيها قيود: لا يجوز تقليص النطاق بحيث يخرج قيد ما خارج الحدود الجديدة
        var entryDates = await _db.JournalEntries.AsNoTracking()
            .Where(e => e.AccountingPeriodId == period.Id && !e.IsDeleted)
            .Select(e => e.EntryDate)
            .ToListAsync(ct);

        if (entryDates.Count > 0)
        {
            var min = entryDates.Min();
            var max = entryDates.Max();
            if (req.StartDate.Date > min.Date || req.EndDate.Date < max.Date)
                throw new DomainException(
                    $"الفترة تحتوي على قيود في الفترة {min:yyyy-MM-dd} → {max:yyyy-MM-dd}؛ " +
                    "لا يمكن تقليص نطاق الفترة لما هو أضيق من تواريخ القيود الموجودة.");
        }

        period.SetStartDate(req.StartDate);
        period.SetEndDate(req.EndDate);
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ─────────────────────────────────────────────────────────────────────────
// 2) حذف فترة محاسبية (إذا لا قيود)
// ─────────────────────────────────────────────────────────────────────────
public record DeleteAccountingPeriodCommand(int PeriodId) : IRequest<Unit>;

public class DeleteAccountingPeriodHandler : IRequestHandler<DeleteAccountingPeriodCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public DeleteAccountingPeriodHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteAccountingPeriodCommand req, CancellationToken ct)
    {
        var period = await _db.AccountingPeriods
            .FirstOrDefaultAsync(p => p.Id == req.PeriodId, ct)
            ?? throw new DomainException("الفترة غير موجودة");

        var fy = await _db.FiscalYears.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == period.FiscalYearId, ct)
            ?? throw new DomainException("السنة المالية غير موجودة");

        if (fy.IsClosed)
            throw new DomainException("لا يمكن حذف فترة في سنة مالية مغلقة");

        var entryCount = await _db.JournalEntries.AsNoTracking()
            .CountAsync(e => e.AccountingPeriodId == period.Id && !e.IsDeleted, ct);

        if (entryCount > 0)
            throw new DomainException(
                $"لا يمكن حذف الفترة لأنها تحتوي على {entryCount} قيد محاسبي.");

        _db.AccountingPeriods.Remove(period);
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ─────────────────────────────────────────────────────────────────────────
// 3) تغيير حالة الفترة (فتح/إغلاق/قفل) — للفترة الفردية
//    targetStatus: 1=Open، 2=Closed، 3=Locked
// ─────────────────────────────────────────────────────────────────────────
public record SetAccountingPeriodStatusCommand(int PeriodId, int TargetStatus) : IRequest<Unit>;

public class SetAccountingPeriodStatusHandler : IRequestHandler<SetAccountingPeriodStatusCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public SetAccountingPeriodStatusHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(SetAccountingPeriodStatusCommand req, CancellationToken ct)
    {
        if (req.TargetStatus is not (1 or 2 or 3))
            throw new DomainException("الحالة المطلوبة غير صالحة");

        var period = await _db.AccountingPeriods
            .FirstOrDefaultAsync(p => p.Id == req.PeriodId, ct)
            ?? throw new DomainException("الفترة غير موجودة");

        var fy = await _db.FiscalYears.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == period.FiscalYearId, ct)
            ?? throw new DomainException("السنة المالية غير موجودة");

        if (fy.IsClosed)
            throw new DomainException("لا يمكن تغيير حالة فترة في سنة مالية مغلقة. افكّ إغلاق السنة أولاً.");

        switch (req.TargetStatus)
        {
            case 1: period.ForceOpen(); break;
            case 2: period.Close(); break;
            case 3: period.Lock(); break;
        }

        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ─────────────────────────────────────────────────────────────────────────
// 4) إغلاق/فتح الفترات الشهرية بالجملة (Bulk) حتى تاريخ معيّن.
//    سيناريوهات الاستخدام:
//      • CloseUpTo : إغلاق كل الفترات التي تنتهي ≤ Date (إغلاق الأشهر السابقة).
//      • OpenFrom  : فتح كل الفترات التي تبدأ ≥ Date.
//    الـ TargetStatus يحدّد الحالة المرغوبة (1=Open، 2=Closed، 3=Locked).
// ─────────────────────────────────────────────────────────────────────────
public enum BulkPeriodMode
{
    CloseUpTo = 1, // ‎اقفل كل الفترات التي EndDate ≤ Date
    OpenFrom  = 2, // ‎افتح كل الفترات التي StartDate ≥ Date
}

public record BulkSetPeriodsStatusCommand(
    int FiscalYearId,
    DateTime Date,
    BulkPeriodMode Mode,
    int TargetStatus  // 1=Open، 2=Closed، 3=Locked
) : IRequest<BulkSetPeriodsResultDto>;

public class BulkSetPeriodsResultDto
{
    public int Affected { get; set; }
    public int Total { get; set; }
    public string Message { get; set; } = default!;
}

public class BulkSetPeriodsStatusHandler : IRequestHandler<BulkSetPeriodsStatusCommand, BulkSetPeriodsResultDto>
{
    private readonly IAccountingDbContext _db;
    public BulkSetPeriodsStatusHandler(IAccountingDbContext db) => _db = db;

    public async Task<BulkSetPeriodsResultDto> Handle(BulkSetPeriodsStatusCommand req, CancellationToken ct)
    {
        if (req.TargetStatus is not (1 or 2 or 3))
            throw new DomainException("الحالة المطلوبة غير صالحة");

        var fy = await _db.FiscalYears.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == req.FiscalYearId, ct)
            ?? throw new DomainException("السنة المالية غير موجودة");

        if (fy.IsClosed)
            throw new DomainException("لا يمكن تعديل الفترات لسنة مالية مغلقة. افكّ إغلاق السنة أولاً.");

        // ‎جلب الفترات المرشَّحة حسب الوضع. التواريخ تُقارَن باليوم فقط.
        var date = req.Date.Date;
        var allPeriods = await _db.AccountingPeriods
            .Where(p => p.FiscalYearId == req.FiscalYearId)
            .ToListAsync(ct);

        var targets = req.Mode switch
        {
            BulkPeriodMode.CloseUpTo => allPeriods.Where(p => p.EndDate.Date <= date).ToList(),
            BulkPeriodMode.OpenFrom  => allPeriods.Where(p => p.StartDate.Date >= date).ToList(),
            _ => throw new DomainException("وضع غير معروف"),
        };

        if (targets.Count == 0)
        {
            return new BulkSetPeriodsResultDto
            {
                Affected = 0,
                Total = allPeriods.Count,
                Message = "لا توجد فترات مطابقة للتاريخ المحدد",
            };
        }

        int changed = 0;
        foreach (var p in targets)
        {
            var current = (int)p.Status;
            if (current == req.TargetStatus) continue; // لا تغيير

            switch (req.TargetStatus)
            {
                case 1: p.ForceOpen(); break;
                case 2: p.Close(); break;
                case 3: p.Lock(); break;
            }
            changed++;
        }

        await _db.SaveChangesAsync(ct);

        var modeLabel = req.Mode == BulkPeriodMode.CloseUpTo
            ? $"حتى {date:yyyy-MM-dd}"
            : $"اعتباراً من {date:yyyy-MM-dd}";
        return new BulkSetPeriodsResultDto
        {
            Affected = changed,
            Total = targets.Count,
            Message = $"تم تحديث {changed} من {targets.Count} فترة ({modeLabel})",
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────
// 5) إعادة مزامنة الفترات الشهرية لتطابق تواريخ السنة المالية الحالية.
//    تُحذَف الفترات الفارغة الواقعة كلياً خارج نطاق السنة، وتُضاف فترات
//    شهرية جديدة لتغطية أي امتداد لم يكن مُغطّى. الفترات التي تحوي قيوداً
//    لا تُحذَف؛ فقط تُعدَّل حدودها قدر الإمكان.
// ─────────────────────────────────────────────────────────────────────────
public record ResyncFiscalYearPeriodsCommand(int FiscalYearId) : IRequest<ResyncPeriodsResultDto>;

public class ResyncPeriodsResultDto
{
    public int Removed { get; set; }
    public int Added { get; set; }
    public int Adjusted { get; set; }
    public int Total { get; set; }
    public string Message { get; set; } = default!;
}

public class ResyncFiscalYearPeriodsHandler : IRequestHandler<ResyncFiscalYearPeriodsCommand, ResyncPeriodsResultDto>
{
    private readonly IAccountingDbContext _db;
    public ResyncFiscalYearPeriodsHandler(IAccountingDbContext db) => _db = db;

    public async Task<ResyncPeriodsResultDto> Handle(ResyncFiscalYearPeriodsCommand req, CancellationToken ct)
    {
        var fy = await _db.FiscalYears
            .Include(f => f.Periods)
            .FirstOrDefaultAsync(f => f.Id == req.FiscalYearId, ct)
            ?? throw new DomainException("السنة المالية غير موجودة");

        if (fy.IsClosed)
            throw new DomainException("لا يمكن إعادة مزامنة فترات سنة مالية مغلقة");

        // ‎جلب القيود لكل فترة لمعرفة الفارغ من المُستخدَم.
        var periodIds = fy.Periods.Select(p => p.Id).ToList();
        var entryStats = new Dictionary<int, (DateTime Min, DateTime Max, int Count)>();
        if (periodIds.Count > 0)
        {
            var stats = await _db.JournalEntries.AsNoTracking()
                .Where(e => periodIds.Contains(e.AccountingPeriodId))
                .GroupBy(e => e.AccountingPeriodId)
                .Select(g => new
                {
                    PeriodId = g.Key,
                    Min = g.Min(e => e.EntryDate),
                    Max = g.Max(e => e.EntryDate),
                    Count = g.Count(),
                })
                .ToListAsync(ct);
            foreach (var s in stats)
                entryStats[s.PeriodId] = (s.Min, s.Max, s.Count);
        }

        // ‎التحقق: لا قيد خارج نطاق السنة.
        if (entryStats.Count > 0)
        {
            var globalMin = entryStats.Values.Min(v => v.Min);
            var globalMax = entryStats.Values.Max(v => v.Max);
            if (globalMin < fy.StartDate)
                throw new DomainException(
                    $"يوجد قيد بتاريخ {globalMin:yyyy-MM-dd} أقدم من بداية السنة. أصلِح القيد أولاً.");
            if (globalMax > fy.EndDate)
                throw new DomainException(
                    $"يوجد قيد بتاريخ {globalMax:yyyy-MM-dd} يتجاوز نهاية السنة. أصلِح القيد أولاً.");
        }

        int removed = 0, adjusted = 0, added = 0;

        // 1) احذف الفترات الفارغة الخارجة كلياً عن النطاق.
        var outOfRange = fy.Periods
            .Where(p => !entryStats.ContainsKey(p.Id) &&
                        (p.EndDate < fy.StartDate || p.StartDate > fy.EndDate))
            .ToList();
        foreach (var p in outOfRange)
        {
            fy.Periods.Remove(p);
            _db.AccountingPeriods.Remove(p);
            removed++;
        }

        // 2) إذا فرغت الفترات بالكامل (مثلاً لا توجد قيود) أعد توليدها.
        if (fy.Periods.Count == 0)
        {
            fy.RegeneratePeriods();
            added = fy.Periods.Count;
        }
        else
        {
            // 3) عدّل حدود الفترة الأولى/الأخيرة لتلامس حدود السنة.
            var ordered = fy.Periods.OrderBy(p => p.StartDate).ToList();
            var first = ordered.First();
            var last = ordered.Last();
            if (first.StartDate != fy.StartDate)
            {
                first.SetStartDate(fy.StartDate);
                adjusted++;
            }
            if (last.EndDate != fy.EndDate)
            {
                last.SetEndDate(fy.EndDate);
                adjusted++;
            }
        }

        // 4) إعادة الترقيم.
        int n = 1;
        foreach (var p in fy.Periods.OrderBy(p => p.StartDate))
            p.SetPeriodNumber(n++);

        await _db.SaveChangesAsync(ct);

        var msg = $"تمّت إعادة المزامنة: حُذِفَت {removed}، أُضيفت {added}، عُدّلت حدود {adjusted}.";
        return new ResyncPeriodsResultDto
        {
            Removed = removed,
            Added = added,
            Adjusted = adjusted,
            Total = fy.Periods.Count,
            Message = msg,
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────
// 6) استعلام حالة الفترة بتاريخ معيّن (للـ frontend ليعرف هل التعديل
//    مسموح به على القيود ضمن هذا التاريخ).
// ─────────────────────────────────────────────────────────────────────────
public record GetPeriodStatusByDateQuery(DateTime Date) : IRequest<PeriodStatusByDateDto?>;

public class GetPeriodStatusByDateHandler : IRequestHandler<GetPeriodStatusByDateQuery, PeriodStatusByDateDto?>
{
    private readonly IAccountingDbContext _db;
    public GetPeriodStatusByDateHandler(IAccountingDbContext db) => _db = db;

    public async Task<PeriodStatusByDateDto?> Handle(GetPeriodStatusByDateQuery req, CancellationToken ct)
    {
        var date = req.Date.Date;
        var period = await _db.AccountingPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date, ct);

        if (period == null) return null;

        var fy = await _db.FiscalYears.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == period.FiscalYearId, ct);

        var statusInt = (int)period.Status;
        var isOpen = period.Status == PeriodStatus.Open;
        var isFyClosed = fy?.IsClosed ?? false;

        return new PeriodStatusByDateDto
        {
            Date = date,
            FiscalYearId = period.FiscalYearId,
            FiscalYearName = fy?.Name ?? "",
            FiscalYearIsClosed = isFyClosed,
            PeriodId = period.Id,
            PeriodNumber = period.PeriodNumber,
            PeriodStartDate = period.StartDate,
            PeriodEndDate = period.EndDate,
            PeriodStatus = statusInt,
            IsEditable = isOpen && !isFyClosed,
        };
    }
}
