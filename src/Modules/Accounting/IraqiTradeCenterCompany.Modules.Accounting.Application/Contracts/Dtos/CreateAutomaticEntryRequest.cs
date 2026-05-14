namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Contracts.Dtos;

/// <summary>
/// طلب إنشاء قيد محاسبي تلقائي من مودول آخر (Store, Inventory).
/// لا يكشف Account IDs - بس أكواد الحسابات (مثل "1.2.01").
/// </summary>
public class CreateAutomaticEntryRequest
{
    public DateTime EntryDate { get; set; }
    public string SourceCode { get; set; } = default!;       // "SalesInvoice", "Payment", "Commission", "StockAdjustment"
    public string Description { get; set; } = default!;
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }
    public List<AutomaticEntryLine> Lines { get; set; } = new();
}

public class AutomaticEntryLine
{
    public string AccountCode { get; set; } = default!;  // 1.1.01, 1.2.01, 4.1.01 ...
    public bool IsDebit { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
