using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;

public record GetFiscalYearsQuery() : IRequest<List<FiscalYearDto>>;

public class GetFiscalYearsHandler : IRequestHandler<GetFiscalYearsQuery, List<FiscalYearDto>>
{
    private readonly IAccountingDbContext _db;
    public GetFiscalYearsHandler(IAccountingDbContext db) => _db = db;

    public async Task<List<FiscalYearDto>> Handle(GetFiscalYearsQuery req, CancellationToken ct)
    {
        var years = await _db.FiscalYears.AsNoTracking().OrderBy(f => f.StartDate).ToListAsync(ct);
        var fyIds = years.Select(y => y.Id).ToList();
        var periods = await _db.AccountingPeriods.AsNoTracking()
            .Where(p => fyIds.Contains(p.FiscalYearId))
            .OrderBy(p => p.FiscalYearId).ThenBy(p => p.PeriodNumber)
            .ToListAsync(ct);

        return years.Select(y => new FiscalYearDto
        {
            Id = y.Id,
            Name = y.Name,
            StartDate = y.StartDate,
            EndDate = y.EndDate,
            IsClosed = y.IsClosed,
            ClosedAt = y.ClosedAt,
            Periods = periods.Where(p => p.FiscalYearId == y.Id).Select(p => new AccountingPeriodDto
            {
                Id = p.Id,
                FiscalYearId = p.FiscalYearId,
                PeriodNumber = p.PeriodNumber,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Status = (int)p.Status,
                StatusText = p.Status.ToString(),
            }).ToList()
        }).ToList();
    }
}
