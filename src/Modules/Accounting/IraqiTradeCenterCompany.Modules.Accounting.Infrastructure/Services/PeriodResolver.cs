using IraqiTradeCenterCompany.Modules.Accounting.Application.Internal;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Exceptions;
using IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Services;

internal class PeriodResolver : IPeriodResolver
{
    private readonly AccountingDbContext _db;
    public PeriodResolver(AccountingDbContext db) => _db = db;

    public async Task<(int FiscalYearId, int PeriodId)> ResolveAsync(DateTime date, CancellationToken ct = default)
    {
        var period = await _db.AccountingPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date, ct);
        if (period == null) throw new DomainException($"لا توجد فترة محاسبية للتاريخ {date:yyyy-MM-dd}");
        if (period.Status == PeriodStatus.Closed || period.Status == PeriodStatus.Locked)
            throw new ClosedPeriodException(date);
        return (period.FiscalYearId, period.Id);
    }
}
