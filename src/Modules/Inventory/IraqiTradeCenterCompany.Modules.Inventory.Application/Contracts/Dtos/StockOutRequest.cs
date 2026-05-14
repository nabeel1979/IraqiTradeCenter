namespace IraqiTradeCenterCompany.Modules.Inventory.Application.Contracts.Dtos;

public class StockOutRequest
{
    public int ItemId { get; set; }
    public int WarehouseId { get; set; }
    public int UnitOfMeasureId { get; set; }
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = default!;
    public int ReferenceId { get; set; }
    public string ReferenceNumber { get; set; } = default!;
}

public class StockReturnRequest
{
    public int ItemId { get; set; }
    public int WarehouseId { get; set; }
    public int UnitOfMeasureId { get; set; }
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = default!;
    public int ReferenceId { get; set; }
    public string ReferenceNumber { get; set; } = default!;
}

/// <summary>صورة مختصرة للمادة - تستخدمها Store عند إنشاء فاتورة</summary>
public class ItemSnapshot
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public int BaseUnitId { get; set; }
    public string BaseUnitName { get; set; } = default!;
    public int? MediumUnitId { get; set; }
    public string? MediumUnitName { get; set; }
    public decimal? MediumUnitFactor { get; set; }
    public int? LargeUnitId { get; set; }
    public string? LargeUnitName { get; set; }
    public decimal? LargeUnitFactor { get; set; }
    public decimal BaseSalesPrice { get; set; }
    public decimal? MediumSalesPrice { get; set; }
    public decimal? LargeSalesPrice { get; set; }
    public decimal AvailableStock { get; set; }
    public bool IsAvailableForSale { get; set; }
}
