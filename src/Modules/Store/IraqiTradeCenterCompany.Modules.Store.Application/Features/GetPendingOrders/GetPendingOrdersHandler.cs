using AutoMapper;
using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Store.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.GetPendingOrders;

public class GetPendingOrdersHandler : IRequestHandler<GetPendingOrdersQuery, PagedResult<IncomingOrderDto>>
{
    private readonly IStoreDbContext _store;
    private readonly IMapper _mapper;
    public GetPendingOrdersHandler(IStoreDbContext store, IMapper mapper) { _store = store; _mapper = mapper; }

    public async Task<PagedResult<IncomingOrderDto>> Handle(GetPendingOrdersQuery req, CancellationToken ct)
    {
        var q = _store.IncomingOrders.AsNoTracking().Include(o => o.Items).AsQueryable();
        if (req.Status.HasValue) q = q.Where(o => o.Status == req.Status);
        else q = q.Where(o => o.Status == OrderProcessingStatus.Pending || o.Status == OrderProcessingStatus.Reviewed);

        var total = await q.CountAsync(ct);
        var orders = await q.OrderByDescending(o => o.ReceivedAt)
            .Skip((req.PageNumber - 1) * req.PageSize).Take(req.PageSize)
            .ToListAsync(ct);

        var customerIds = orders.Select(o => o.CustomerId).Distinct().ToList();
        var customers = await _store.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.BusinessName, ct);

        var dtos = _mapper.Map<List<IncomingOrderDto>>(orders);
        foreach (var d in dtos)
            d.CustomerName = customers.GetValueOrDefault(d.CustomerId);

        return new PagedResult<IncomingOrderDto>
        {
            Items = dtos, TotalCount = total,
            PageNumber = req.PageNumber, PageSize = req.PageSize
        };
    }
}
