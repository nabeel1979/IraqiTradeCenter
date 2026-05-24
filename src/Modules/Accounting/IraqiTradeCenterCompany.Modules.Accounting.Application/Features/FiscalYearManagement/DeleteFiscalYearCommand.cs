using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;

public record DeleteFiscalYearCommand(int Id) : IRequest<Unit>;

public class DeleteFiscalYearHandler : IRequestHandler<DeleteFiscalYearCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public DeleteFiscalYearHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteFiscalYearCommand req, CancellationToken ct)
    {
        var fy = await _db.FiscalYears
            .Include(f => f.Periods)
            .FirstOrDefaultAsync(f => f.Id == req.Id, ct)
            ?? throw new DomainException("السنة المالية غير موجودة");

        if (fy.IsClosed)
            throw new DomainException("لا يمكن حذف سنة مالية مغلقة");

        // ‎فحص: لا يجوز الحذف إذا كانت السنة لها قيود (حتى المسودات)
        var periodIds = fy.Periods.Select(p => p.Id).ToList();
        if (periodIds.Count > 0)
        {
            var entryCount = await _db.JournalEntries.AsNoTracking()
                .CountAsync(e => periodIds.Contains(e.AccountingPeriodId), ct);
            if (entryCount > 0)
                throw new DomainException(
                    $"لا يمكن حذف السنة \"{fy.Name}\" لأنها تحتوي على {entryCount} قيد محاسبي. " +
                    "احذف القيود أولاً أو انقلها إلى سنة أخرى.");
        }

        // ‎حذف فعلي للفترات (بدون قيود مرتبطة) + soft-delete للسنة
        foreach (var p in fy.Periods.ToList()) _db.AccountingPeriods.Remove(p);
        fy.MarkAsDeleted();

        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
