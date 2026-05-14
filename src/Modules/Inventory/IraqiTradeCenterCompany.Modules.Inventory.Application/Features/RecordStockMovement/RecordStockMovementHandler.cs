using IraqiTradeCenterCompany.Modules.Inventory.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Inventory.Domain.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Inventory.Application.Features.RecordStockMovement;

public class RecordStockMovementHandler : IRequestHandler<RecordStockMovementCommand, Result<int>>
{
    private readonly IInventoryDbContext _db;
    public RecordStockMovementHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<int>> Handle(RecordStockMovementCommand req, CancellationToken ct)
    {
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == req.ItemId, ct);
        if (item == null) return Result.Failure<int>("المادة غير موجودة");

        var factor = item.ConvertToBase(req.UnitOfMeasureId, 1m);
        try
        {
            var mv = StockMovement.Create(item, req.WarehouseId, req.Type,
                req.UnitOfMeasureId, req.Quantity, factor,
                refNumber: req.ReferenceNumber, unitCost: req.UnitCost, notes: req.Notes);
            await _db.StockMovements.AddAsync(mv, ct);
            await _db.SaveChangesAsync(ct);
            return Result.Success(mv.Id);
        }
        catch (InsufficientStockException ex) { return Result.Failure<int>(ex.Message); }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }
}
