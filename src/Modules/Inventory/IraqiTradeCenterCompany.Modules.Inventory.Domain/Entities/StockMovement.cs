using IraqiTradeCenterCompany.Modules.Inventory.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;

public class StockMovement : BaseEntity
{
    public DateTime MovementDate { get; private set; }
    public int ItemId { get; private set; }
    public int WarehouseId { get; private set; }
    public StockMovementType Type { get; private set; }
    public int UnitOfMeasureId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal ConversionFactor { get; private set; }
    public decimal QuantityInBase { get; private set; }
    public decimal QuantityBefore { get; private set; }
    public decimal QuantityAfter { get; private set; }
    public decimal? UnitCost { get; private set; }
    public decimal? TotalValue { get; private set; }
    public string? ReferenceType { get; private set; }
    public int? ReferenceId { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? Notes { get; private set; }

    private StockMovement() { }

    public static StockMovement Create(Item item, int warehouseId, StockMovementType type,
        int unitId, decimal quantity, decimal conversionFactor,
        string? refType = null, int? refId = null, string? refNumber = null,
        decimal? unitCost = null, string? notes = null)
    {
        if (quantity <= 0) throw new DomainException("الكمية لازم موجبة");

        var qtyInBase = quantity * conversionFactor;
        var qtyBefore = item.StockBaseQuantity;

        decimal delta = type switch
        {
            StockMovementType.PurchaseIn => qtyInBase,
            StockMovementType.SalesReturn => qtyInBase,
            StockMovementType.OpeningBalance => qtyInBase,
            StockMovementType.SalesOut => -qtyInBase,
            StockMovementType.PurchaseReturn => -qtyInBase,
            StockMovementType.Damaged => -qtyInBase,
            StockMovementType.Adjustment => qtyInBase,
            _ => 0
        };

        item.AdjustStock(delta);

        return new StockMovement
        {
            MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = warehouseId, Type = type,
            UnitOfMeasureId = unitId, Quantity = quantity, ConversionFactor = conversionFactor,
            QuantityInBase = qtyInBase, QuantityBefore = qtyBefore, QuantityAfter = item.StockBaseQuantity,
            UnitCost = unitCost,
            TotalValue = unitCost.HasValue ? unitCost.Value * qtyInBase : null,
            ReferenceType = refType, ReferenceId = refId, ReferenceNumber = refNumber,
            Notes = notes
        };
    }
}
