using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.GetCustomersList;

public class GetCustomersListHandler : IRequestHandler<GetCustomersListQuery, PagedResult<CustomerDto>>
{
    private readonly IStoreDbContext _store;
    public GetCustomersListHandler(IStoreDbContext store) => _store = store;

    public async Task<PagedResult<CustomerDto>> Handle(GetCustomersListQuery req, CancellationToken ct)
    {
        var q = _store.Customers.AsNoTracking();
        if (req.ActiveOnly == true) q = q.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(req.SearchTerm))
        {
            var t = req.SearchTerm.Trim();
            q = q.Where(c => c.BusinessName.Contains(t) || c.OwnerName.Contains(t)
                          || c.Phone.Contains(t) || c.Code.Contains(t));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(c => c.BusinessName)
            .Skip((req.PageNumber - 1) * req.PageSize).Take(req.PageSize)
            .Select(c => new CustomerDto
            {
                Id = c.Id,
                Code = c.Code,
                BusinessName = c.BusinessName,
                OwnerName = c.OwnerName,
                Phone = c.Phone,
                Email = c.Email,
                Address = c.Address,
                CreditLimit = c.CreditLimit,
                CurrentBalance = c.CurrentBalance,
                AssignedSalesRepId = c.AssignedSalesRepId,
                IsActive = c.IsActive
            })
            .ToListAsync(ct);

        return new PagedResult<CustomerDto>
        {
            Items = items, TotalCount = total,
            PageNumber = req.PageNumber, PageSize = req.PageSize
        };
    }
}
