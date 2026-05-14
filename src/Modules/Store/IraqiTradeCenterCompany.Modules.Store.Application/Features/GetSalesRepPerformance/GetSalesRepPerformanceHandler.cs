using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Store.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.GetSalesRepPerformance;

public class GetSalesRepPerformanceHandler : IRequestHandler<GetSalesRepPerformanceQuery, Result<SalesRepPerformanceDto>>
{
    private readonly IStoreDbContext _store;
    public GetSalesRepPerformanceHandler(IStoreDbContext store) => _store = store;

    public async Task<Result<SalesRepPerformanceDto>> Handle(GetSalesRepPerformanceQuery req, CancellationToken ct)
    {
        var rep = await _store.SalesReps.Include(r => r.Tiers)
            .FirstOrDefaultAsync(r => r.Id == req.SalesRepId, ct);
        if (rep == null) return Result.Failure<SalesRepPerformanceDto>("المندوب غير موجود");

        var invoices = await _store.SalesInvoices.AsNoTracking()
            .Where(i => i.SalesRepId == req.SalesRepId
                     && i.InvoiceDate >= req.FromDate && i.InvoiceDate <= req.ToDate
                     && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft)
            .Select(i => i.TotalAmount).ToListAsync(ct);

        var total = invoices.Sum();
        var commission = rep.CalculateCommission(total);

        return Result.Success(new SalesRepPerformanceDto
        {
            SalesRepId = rep.Id, FullName = rep.FullName,
            FromDate = req.FromDate, ToDate = req.ToDate,
            TotalSales = total, InvoiceCount = invoices.Count,
            CalculatedCommission = commission
        });
    }
}
