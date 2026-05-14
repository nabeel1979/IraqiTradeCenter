using IraqiTradeCenterCompany.Modules.Inventory.Domain.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;

public class Item : BaseEntity
{
    public string Code { get; private set; } = default!;
    public string Barcode { get; private set; } = default!;
    public string NameAr { get; private set; } = default!;
    public string? NameEn { get; private set; }
    public string? Description { get; private set; }
    public int? CategoryId { get; private set; }

    public int BaseUnitId { get; private set; }
    public int? MediumUnitId { get; private set; }
    public decimal? MediumUnitFactor { get; private set; }
    public int? LargeUnitId { get; private set; }
    public decimal? LargeUnitFactor { get; private set; }

    public decimal PurchasePrice { get; private set; }
    public decimal BaseSalesPrice { get; private set; }
    public decimal? MediumSalesPrice { get; private set; }
    public decimal? LargeSalesPrice { get; private set; }

    public decimal StockBaseQuantity { get; private set; }
    public decimal MinimumStockLevel { get; private set; }
    public decimal MaximumStockLevel { get; private set; }

    public bool IsActive { get; private set; }
    public bool IsAvailableForSale { get; private set; }
    public byte[]? RowVersion { get; private set; }

    private Item() { }

    public static Item Create(string code, string barcode, string nameAr, int baseUnitId,
                              decimal purchasePrice, decimal baseSalesPrice)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("رمز المادة مطلوب");
        if (purchasePrice < 0) throw new DomainException("سعر الشراء سالب");
        if (baseSalesPrice < purchasePrice) throw new DomainException("سعر البيع لا يقل عن الشراء");
        return new Item
        {
            Code = code, Barcode = barcode, NameAr = nameAr,
            BaseUnitId = baseUnitId,
            PurchasePrice = purchasePrice, BaseSalesPrice = baseSalesPrice,
            IsActive = true, IsAvailableForSale = true
        };
    }

    public void SetMediumUnit(int unitId, decimal factor, decimal price)
    {
        if (factor <= 0) throw new DomainException("معامل التحويل أكبر من صفر");
        MediumUnitId = unitId; MediumUnitFactor = factor; MediumSalesPrice = price;
    }

    public void SetLargeUnit(int unitId, decimal factor, decimal price)
    {
        if (factor <= 0) throw new DomainException("معامل التحويل أكبر من صفر");
        if (!MediumUnitId.HasValue) throw new DomainException("لازم تحدد الوحدة المتوسطة قبل الكبيرة");
        LargeUnitId = unitId; LargeUnitFactor = factor; LargeSalesPrice = price;
    }

    /// <summary>Internal - يستخدمه StockMovement فقط</summary>
    internal void AdjustStock(decimal deltaInBase)
    {
        var newQty = StockBaseQuantity + deltaInBase;
        if (newQty < 0)
            throw new InsufficientStockException(NameAr, Math.Abs(deltaInBase), StockBaseQuantity);
        StockBaseQuantity = newQty;
    }

    public void SetStockLevels(decimal min, decimal max) { MinimumStockLevel = min; MaximumStockLevel = max; }
    public void SetOpeningStock(decimal q) => StockBaseQuantity = q;

    public decimal ConvertToBase(int unitId, decimal quantity)
    {
        if (unitId == BaseUnitId) return quantity;
        if (unitId == MediumUnitId && MediumUnitFactor.HasValue)
            return quantity * MediumUnitFactor.Value;
        if (unitId == LargeUnitId && LargeUnitFactor.HasValue && MediumUnitFactor.HasValue)
            return quantity * LargeUnitFactor.Value * MediumUnitFactor.Value;
        throw new DomainException("وحدة قياس غير صالحة لهذه المادة");
    }

    public decimal GetSalesPriceForUnit(int unitId)
    {
        if (unitId == BaseUnitId) return BaseSalesPrice;
        if (unitId == MediumUnitId && MediumSalesPrice.HasValue) return MediumSalesPrice.Value;
        if (unitId == LargeUnitId && LargeSalesPrice.HasValue) return LargeSalesPrice.Value;
        throw new DomainException("لا يوجد سعر لهذه الوحدة");
    }
}
