using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;

/// <summary>
/// فك إغلاق سنة مالية مغلقة وإعادة فتحها (مع جميع فتراتها).
/// تستخدم هذه الميزة في حالات التصحيح أو إعادة العمل على بيانات سنة مغلقة بالخطأ.
///
/// قيود السلامة:
///   • إذا كانت السنة قد دُوِّرت أرصدتها إلى سنة لاحقة (وُجد قيد افتتاحي مدخل
///     من sp_FY_Rollover في سنة لاحقة)، فلا يجوز فك الإغلاق إلا بعد حذف ذلك
///     القيد الافتتاحي يدوياً (تجنّب أرصدة افتتاحية مكرَّرة/متضاربة).
/// </summary>
public record ReopenFiscalYearCommand(int FiscalYearId) : IRequest<Unit>;

public class ReopenFiscalYearHandler : IRequestHandler<ReopenFiscalYearCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public ReopenFiscalYearHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(ReopenFiscalYearCommand req, CancellationToken ct)
    {
        var fy = await _db.FiscalYears
            .Include(f => f.Periods)
            .FirstOrDefaultAsync(f => f.Id == req.FiscalYearId, ct)
            ?? throw new DomainException("السنة المالية غير موجودة");

        if (!fy.IsClosed) throw new DomainException("السنة المالية ليست مغلقة");

        // ‎فحص: هل وُجد قيد افتتاحي في سنة لاحقة مرتبط بهذه السنة (تدوير)؟
        // ‎نتعرّف على ذلك من خلال JournalEntries من النوع Opening (=2) ضمن
        // ‎فترات سنة تالية، تحمل وصفاً يُشير إلى سنتنا. التشخيص بسيط ومحافظ:
        // ‎نمنع فك الإغلاق إذا أيّ سنة لاحقة فيها قيود Opening، ونرشد المستخدم
        // ‎لحذفها أوّلاً.
        var laterFyHasOpening = await _db.FiscalYears.AsNoTracking()
            .Where(f => f.StartDate > fy.EndDate)
            .AnyAsync(f => _db.AccountingPeriods.AsNoTracking()
                .Where(p => p.FiscalYearId == f.Id)
                .Any(p => _db.JournalEntries.AsNoTracking()
                    .Any(e => e.AccountingPeriodId == p.Id && (int)e.EntryType == 2 && !e.IsDeleted)), ct);

        if (laterFyHasOpening)
        {
            throw new DomainException(
                "لا يمكن فك إغلاق السنة لأنه تم تدوير أرصدتها إلى سنة لاحقة. " +
                "احذف القيد الافتتاحي في السنة اللاحقة أولاً ثم أعد المحاولة.");
        }

        fy.Reopen();
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
