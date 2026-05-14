using IraqiTradeCenterCompany.Modules.Inventory.Application.Contracts;
using IraqiTradeCenterCompany.Modules.Inventory.Application.Contracts.Dtos;
using IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Inventory.Domain.Enums;
using IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Services;

/// <summary>
/// التطبيق الفعلي للـ IInventoryService - Store يستدعيها عند إصدار فاتورة
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly InventoryDbContext _db;
    public InventoryService(InventoryDbContext db) => _db = db;

    public async Task<bool> CheckStockAvailabilityAsync(int itemId, int unitId, decimal quantity, CancellationToken ct = default)
    {
        var item = await _db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item == null) return false;
        var qtyBase = item.ConvertToBase(unitId, quantity);
        return item.StockBaseQuantity >= qtyBase;
    }

    public async Task<int> RecordSalesOutAsync(StockOutRequest req, CancellationToken ct = default)
    {
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == req.ItemId, ct)
            ?? throw new DomainException("المادة غير موجودة");

        var factor = item.ConvertToBase(req.UnitOfMeasureId, 1m);
        var mv = StockMovement.Create(item, req.WarehouseId, StockMovementType.SalesOut,
            req.UnitOfMeasureId, req.Quantity, factor,
            refType: req.ReferenceType, refId: req.ReferenceId, refNumber: req.ReferenceNumber);

        await _db.StockMovements.AddAsync(mv, ct);
        await _db.SaveChangesAsync(ct);
        return mv.Id;
    }

    public async Task<int> RecordSalesReturnAsync(StockReturnRequest req, CancellationToken ct = default)
    {
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == req.ItemId, ct)
            ?? throw new DomainException("المادة غير موجودة");

        var factor = item.ConvertToBase(req.UnitOfMeasureId, 1m);
        var mv = StockMovement.Create(item, req.WarehouseId, StockMovementType.SalesReturn,
            req.UnitOfMeasureId, req.Quantity, factor,
            refType: req.ReferenceType, refId: req.ReferenceId, refNumber: req.ReferenceNumber);

        await _db.StockMovements.AddAsync(mv, ct);
        await _db.SaveChangesAsync(ct);
        return mv.Id;
    }

    public async Task<ItemSnapshot?> GetItemSnapshotAsync(int itemId, CancellationToken ct = default)
    {
        var item = await _db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item == null) return null;

        var unitIds = new List<int> { item.BaseUnitId };
        if (item.MediumUnitId.HasValue) unitIds.Add(item.MediumUnitId.Value);
        if (item.LargeUnitId.HasValue) unitIds.Add(item.LargeUnitId.Value);
        var units = await _db.UnitsOfMeasure.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.NameAr, ct);

        return new ItemSnapshot
        {
            Id = item.Id, Code = item.Code, NameAr = item.NameAr,
            BaseUnitId = item.BaseUnitId, BaseUnitName = units[item.BaseUnitId],
            MediumUnitId = item.MediumUnitId, MediumUnitFactor = item.MediumUnitFactor,
            MediumUnitName = item.MediumUnitId.HasValue && units.ContainsKey(item.MediumUnitId.Value)
                ? units[item.MediumUnitId.Value] : null,
            LargeUnitId = item.LargeUnitId, LargeUnitFactor = item.LargeUnitFactor,
            LargeUnitName = item.LargeUnitId.HasValue && units.ContainsKey(item.LargeUnitId.Value)
                ? units[item.LargeUnitId.Value] : null,
            BaseSalesPrice = item.BaseSalesPrice,
            MediumSalesPrice = item.MediumSalesPrice,
            LargeSalesPrice = item.LargeSalesPrice,
            AvailableStock = item.StockBaseQuantity,
            IsAvailableForSale = item.IsAvailableForSale
        };
    }

    public async Task<int?> GetDefaultWarehouseIdAsync(CancellationToken ct = default)
    {
        var wh = await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.IsDefault, ct);
        return wh?.Id;
    }
}
