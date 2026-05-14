namespace IraqiTradeCenterCompany.Modules.Inventory.Application.Dtos;

public class ItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string Barcode { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public decimal PurchasePrice { get; set; }
    public decimal BaseSalesPrice { get; set; }
    public decimal StockBaseQuantity { get; set; }
    public decimal MinimumStockLevel { get; set; }
    public bool IsAvailableForSale { get; set; }
    public bool IsLowStock => StockBaseQuantity <= MinimumStockLevel;
}

public class StockMovementDto
{
    public int Id { get; set; }
    public DateTime MovementDate { get; set; }
    public string ItemName { get; set; } = default!;
    public string TypeName { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal QuantityInBase { get; set; }
    public decimal QuantityBefore { get; set; }
    public decimal QuantityAfter { get; set; }
    public string? ReferenceNumber { get; set; }
}
