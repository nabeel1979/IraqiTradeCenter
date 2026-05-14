using AutoMapper;
using AutoMapper.QueryableExtensions;
using IraqiTradeCenterCompany.Modules.Inventory.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Inventory.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Inventory.Application.Features.GetItemsList;

public class GetItemsListHandler : IRequestHandler<GetItemsListQuery, PagedResult<ItemDto>>
{
    private readonly IInventoryDbContext _db;
    private readonly IMapper _mapper;
    public GetItemsListHandler(IInventoryDbContext db, IMapper mapper) { _db = db; _mapper = mapper; }

    public async Task<PagedResult<ItemDto>> Handle(GetItemsListQuery req, CancellationToken ct)
    {
        var q = _db.Items.AsNoTracking().Where(i => i.IsActive);
        if (!string.IsNullOrWhiteSpace(req.SearchTerm))
        {
            var t = req.SearchTerm.Trim();
            q = q.Where(i => i.NameAr.Contains(t) || i.Code.Contains(t) || i.Barcode.Contains(t));
        }
        if (req.CategoryId.HasValue) q = q.Where(i => i.CategoryId == req.CategoryId);
        if (req.LowStockOnly == true) q = q.Where(i => i.StockBaseQuantity <= i.MinimumStockLevel);

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(i => i.NameAr)
            .Skip((req.PageNumber - 1) * req.PageSize).Take(req.PageSize)
            .ProjectTo<ItemDto>(_mapper.ConfigurationProvider).ToListAsync(ct);

        return new PagedResult<ItemDto>
        {
            Items = items, TotalCount = total,
            PageNumber = req.PageNumber, PageSize = req.PageSize
        };
    }
}
