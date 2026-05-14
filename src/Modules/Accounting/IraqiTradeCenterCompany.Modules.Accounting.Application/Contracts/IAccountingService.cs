using IraqiTradeCenterCompany.Modules.Accounting.Application.Contracts.Dtos;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Contracts;

/// <summary>
/// الواجهة العامة لمودول المحاسبة.
/// كل مودول ثاني يحتاج ينشئ قيود (Store, Inventory) يستخدم هذي الواجهة فقط.
/// </summary>
public interface IAccountingService
{
    /// <summary>إنشاء قيد محاسبي تلقائي - يولد القيد ويرحّله ويحفظه</summary>
    Task<int> CreateAutomaticJournalEntryAsync(CreateAutomaticEntryRequest request, CancellationToken ct = default);

    /// <summary>تحويل كود حساب إلى Id (للاستعلام)</summary>
    Task<int> GetAccountIdByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>التأكد من أن الفترة مفتوحة للتاريخ المعطى (يرمي خطأ إذا مغلقة)</summary>
    Task EnsurePeriodOpenAsync(DateTime date, CancellationToken ct = default);
}
