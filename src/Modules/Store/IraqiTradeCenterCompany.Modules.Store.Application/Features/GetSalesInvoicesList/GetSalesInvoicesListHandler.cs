using AutoMapper;
using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Store.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.GetSalesInvoicesList;

public class GetSalesInvoicesListHandler : IRequestHandler<GetSalesInvoicesListQuery, PagedResult<SalesInvoiceDto>>
{
    private readonly IStoreDbContext _store;
    private readonly IMapper _mapper;

    public GetSalesInvoicesListHandler(IStoreDbContext store, IMapper mapper)
    {
        _store = store; _mapper = mapper;
    }

    public async Task<PagedResult<SalesInvoiceDto>> Handle(GetSalesInvoicesListQuery req, CancellationToken ct)
    {
        var q = _store.SalesInvoices.AsNoTracking().Include(i => i.Lines).AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.SearchTerm))
        {
            var t = req.SearchTerm.Trim();
            q = q.Where(i => i.InvoiceNumber.Contains(t));
        }
        if (!string.IsNullOrWhiteSpace(req.Status) &&
            Enum.TryParse<InvoiceStatus>(req.Status, true, out var s))
            q = q.Where(i => i.Status == s);
        if (req.CustomerId.HasValue) q = q.Where(i => i.CustomerId == req.CustomerId);
        if (req.FromDate.HasValue)   q = q.Where(i => i.InvoiceDate >= req.FromDate);
        if (req.ToDate.HasValue)     q = q.Where(i => i.InvoiceDate <= req.ToDate);

        var total = await q.CountAsync(ct);
        var invoices = await q.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Skip((req.PageNumber - 1) * req.PageSize).Take(req.PageSize)
            .ToListAsync(ct);

        // جلب أسماء العملاء
        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await _store.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.BusinessName, ct);

        var dtos = _mapper.Map<List<SalesInvoiceDto>>(invoices);
        foreach (var d in dtos)
            d.CustomerName = customers.GetValueOrDefault(d.CustomerId);

        return new PagedResult<SalesInvoiceDto>
        {
            Items = dtos, TotalCount = total,
            PageNumber = req.PageNumber, PageSize = req.PageSize
        };
    }
}
