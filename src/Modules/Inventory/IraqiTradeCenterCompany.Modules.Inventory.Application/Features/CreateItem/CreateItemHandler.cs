using AutoMapper;
using IraqiTradeCenterCompany.Modules.Inventory.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Inventory.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Inventory.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Inventory.Application.Features.CreateItem;

public class CreateItemHandler : IRequestHandler<CreateItemCommand, Result<ItemDto>>
{
    private readonly IInventoryDbContext _db;
    private readonly IMapper _mapper;
    public CreateItemHandler(IInventoryDbContext db, IMapper mapper) { _db = db; _mapper = mapper; }

    public async Task<Result<ItemDto>> Handle(CreateItemCommand req, CancellationToken ct)
    {
        try
        {
            if (await _db.Items.AnyAsync(i => i.Code == req.Code, ct))
                return Result.Failure<ItemDto>("رمز المادة مسجل مسبقاً");

            var item = Item.Create(req.Code, req.Barcode, req.NameAr,
                req.BaseUnitId, req.PurchasePrice, req.BaseSalesPrice);

            if (req.MediumUnitId.HasValue && req.MediumUnitFactor.HasValue)
                item.SetMediumUnit(req.MediumUnitId.Value, req.MediumUnitFactor.Value,
                    req.MediumSalesPrice ?? req.BaseSalesPrice * req.MediumUnitFactor.Value);

            if (req.LargeUnitId.HasValue && req.LargeUnitFactor.HasValue)
                item.SetLargeUnit(req.LargeUnitId.Value, req.LargeUnitFactor.Value,
                    req.LargeSalesPrice ?? req.BaseSalesPrice * (req.MediumUnitFactor ?? 1) * req.LargeUnitFactor.Value);

            if (req.MinimumStockLevel > 0) item.SetStockLevels(req.MinimumStockLevel, req.MinimumStockLevel * 10);

            await _db.Items.AddAsync(item, ct);
            await _db.SaveChangesAsync(ct);

            if (req.OpeningStock > 0)
            {
                var defaultWh = await _db.Warehouses.FirstOrDefaultAsync(w => w.IsDefault, ct);
                if (defaultWh != null)
                {
                    var mv = StockMovement.Create(item, defaultWh.Id,
                        StockMovementType.OpeningBalance, req.BaseUnitId, req.OpeningStock, 1m,
                        notes: "رصيد افتتاحي");
                    await _db.StockMovements.AddAsync(mv, ct);
                    await _db.SaveChangesAsync(ct);
                }
            }

            return Result.Success(_mapper.Map<ItemDto>(item));
        }
        catch (DomainException ex) { return Result.Failure<ItemDto>(ex.Message); }
    }
}
