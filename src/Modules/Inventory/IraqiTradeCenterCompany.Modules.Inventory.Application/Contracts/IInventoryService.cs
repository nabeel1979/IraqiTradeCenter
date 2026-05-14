using IraqiTradeCenterCompany.Modules.Inventory.Application.Contracts.Dtos;

namespace IraqiTradeCenterCompany.Modules.Inventory.Application.Contracts;

/// <summary>
/// واجهة عامة لمودول المستودعات.
/// Store يستخدمها عند إنشاء فاتورة (يستفسر + يخصم مخزون).
/// </summary>
public interface IInventoryService
{
    /// <summary>التحقق من توفر المخزون</summary>
    Task<bool> CheckStockAvailabilityAsync(int itemId, int unitId, decimal quantity, CancellationToken ct = default);

    /// <summary>تسجيل إخراج مبيعات + تعديل مخزون (داخل Transaction المستدعي)</summary>
    Task<int> RecordSalesOutAsync(StockOutRequest request, CancellationToken ct = default);

    /// <summary>تسجيل إرجاع مبيعات</summary>
    Task<int> RecordSalesReturnAsync(StockReturnRequest request, CancellationToken ct = default);

    /// <summary>صورة المادة (للسعر والوحدات والمخزون المتاح)</summary>
    Task<ItemSnapshot?> GetItemSnapshotAsync(int itemId, CancellationToken ct = default);

    /// <summary>المخزن الافتراضي (للعمليات السريعة)</summary>
    Task<int?> GetDefaultWarehouseIdAsync(CancellationToken ct = default);
}
