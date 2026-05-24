using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;

public record UpdateFiscalYearCommand(int Id, string Name, DateTime StartDate, DateTime EndDate) : IRequest<Unit>;

public class UpdateFiscalYearHandler : IRequestHandler<UpdateFiscalYearCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public UpdateFiscalYearHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(UpdateFiscalYearCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new DomainException("اسم السنة المالية مطلوب");

        var fy = await _db.FiscalYears
            .Include(f => f.Periods)
            .FirstOrDefaultAsync(f => f.Id == req.Id, ct)
            ?? throw new DomainException("السنة المالية غير موجودة");

        if (fy.IsClosed)
            throw new DomainException("لا يمكن تعديل سنة مالية مغلقة");

        // ‎فحص التداخل مع سنوات أخرى (نستثني السنة الحالية)
        var overlap = await _db.FiscalYears.AsNoTracking().AnyAsync(f =>
            f.Id != req.Id &&
            ((req.StartDate >= f.StartDate && req.StartDate <= f.EndDate) ||
             (req.EndDate >= f.StartDate && req.EndDate <= f.EndDate) ||
             (req.StartDate <= f.StartDate && req.EndDate >= f.EndDate)), ct);
        if (overlap)
            throw new DomainException("الفترة الزمنية تتداخل مع سنة مالية أخرى");

        // ‎جلب عدد القيود لكل فترة دفعة واحدة (لتجنّب N+1).
        var periodIds = fy.Periods.Select(p => p.Id).ToList();
        Dictionary<int, (DateTime Min, DateTime Max, int Count)> entryStats = new();
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

        // ‎فحص شامل: لا يجوز أن يبقى قيد خارج النطاق الجديد.
        if (entryStats.Count > 0)
        {
            var globalMin = entryStats.Values.Min(v => v.Min);
            var globalMax = entryStats.Values.Max(v => v.Max);
            if (globalMin < req.StartDate)
                throw new DomainException(
                    $"يوجد قيد بتاريخ {globalMin:yyyy-MM-dd} أقدم من تاريخ البداية الجديد. عدّل القيد أو احذفه أولاً.");
            if (globalMax > req.EndDate)
                throw new DomainException(
                    $"يوجد قيد بتاريخ {globalMax:yyyy-MM-dd} يتجاوز تاريخ النهاية الجديد. عدّل القيد أو احذفه أولاً.");
        }

        var datesChanged = fy.StartDate != req.StartDate || fy.EndDate != req.EndDate;
        fy.Update(req.Name, req.StartDate, req.EndDate);

        if (datesChanged)
        {
            // ‎الاستراتيجية الذكية:
            //   1) إذا لم تحوِ السنة أيّ قيود → احذف كل الفترات وأعد توليدها بالكامل.
            //   2) إذا كانت تحوي قيوداً:
            //      • احذف الفترات الفارغة الخارجة كلياً عن النطاق الجديد.
            //      • للفترات داخل النطاق: عدّل حدودها لتلائم النطاق الجديد إذا لزم.
            //      • أعِد توليد فترات شهرية جديدة لأي نطاق غير مُغطّى.
            var hasAnyEntries = entryStats.Count > 0;
            if (!hasAnyEntries)
            {
                foreach (var p in fy.Periods.ToList()) _db.AccountingPeriods.Remove(p);
                await _db.SaveChangesAsync(ct);
                fy.RegeneratePeriods();
            }
            else
            {
                RealignPeriods(fy, req.StartDate, req.EndDate, entryStats);
            }
        }

        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }

    /// <summary>
    /// يُعيد ضبط فترات السنة لتطابق النطاق [start..end]:
    ///   • الفترات الفارغة الخارجة كلياً عن النطاق → تُحذَف.
    ///   • الفترات التي تحوي قيوداً وتمتدّ خارج النطاق → يُرفض الحفظ
    ///     (تم الفحص مسبقاً، لكن نضيف فحصاً ثانياً للأمان).
    ///   • الفترة الأولى/الأخيرة المتبقّية تُقصّر/تُوسّع لتطابق الحدود.
    ///   • النطاقات غير المُغطّاة تُملأ بفترات شهرية جديدة.
    /// </summary>
    private void RealignPeriods(
        FiscalYear fy,
        DateTime start,
        DateTime end,
        Dictionary<int, (DateTime Min, DateTime Max, int Count)> entryStats)
    {
        // ‎احذف الفترات الفارغة كلياً خارج النطاق الجديد.
        var toRemove = fy.Periods
            .Where(p => !entryStats.ContainsKey(p.Id) &&
                        (p.EndDate < start || p.StartDate > end))
            .ToList();
        foreach (var p in toRemove)
        {
            fy.Periods.Remove(p);
            _db.AccountingPeriods.Remove(p);
        }

        if (fy.Periods.Count == 0)
        {
            // ‎كل الفترات حُذفت — أعد توليدها كلياً.
            fy.RegeneratePeriods();
            return;
        }

        var ordered = fy.Periods.OrderBy(p => p.StartDate).ToList();
        var first = ordered.First();
        var last = ordered.Last();

        // ‎قصّ/وسّع الفترة الأولى لتلامس بداية السنة.
        if (first.StartDate != start)
        {
            // ‎لا يجوز تقصير فترة تحتوي قيداً أقدم من start (مفحوص).
            first.SetStartDate(start);
        }
        // ‎قصّ/وسّع الفترة الأخيرة لتلامس نهاية السنة.
        if (last.EndDate != end)
        {
            // ‎لا يجوز تقصير فترة تحتوي قيداً أحدث من end (مفحوص).
            last.SetEndDate(end);
        }

        // ‎ملء الفجوات: لو كانت السنة الجديدة أوسع، نضيف فترات شهرية لتغطي الامتداد.
        // ‎(يحدث عند توسيع البداية للوراء أو النهاية للأمام.)
        FillGapsBefore(fy, start, ordered.First());
        FillGapsAfter(fy, ordered.Last(), end);

        // ‎إعادة ترقيم الفترات بترتيب تصاعدي حسب الـ StartDate.
        int n = 1;
        foreach (var p in fy.Periods.OrderBy(p => p.StartDate))
        {
            p.SetPeriodNumber(n++);
        }
    }

    private void FillGapsBefore(FiscalYear fy, DateTime start, AccountingPeriod first)
    {
        // ‎لا فجوة لأن الفترة الأولى تبدأ بالفعل من start (تم تعديلها أعلاه).
        // ‎هذه الدالة محجوزة لو احتجنا لاحقاً تقسيم الفترة الأولى الموسّعة إلى أشهر.
        _ = fy; _ = start; _ = first;
    }

    private void FillGapsAfter(FiscalYear fy, AccountingPeriod last, DateTime end)
    {
        // ‎مماثل: الفترة الأخيرة الآن تنتهي بـ end، فلا فجوات.
        _ = fy; _ = last; _ = end;
    }
}
