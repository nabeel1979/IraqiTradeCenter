namespace IraqiTradeCenterCompany.API.Settings;

/// <summary>
/// عملة في النظام. تُدار مركزياً عبر إعدادات الشركة:
///  - IsEnabled: متاحة للاستخدام في القيود/الفواتير.
///  - IsBase:    العملة الرئيسية للنظام (واحدة فقط في كل وقت).
/// </summary>
public class Currency
{
    /// <summary>كود ISO 4217 الحرفي (مثل IQD, USD, EUR) - PK</summary>
    public string Code { get; set; } = default!;
    /// <summary>الرقم العالمي ISO 4217 (3 أرقام، مثل 368 لـ IQD، 840 لـ USD)</summary>
    public string? NumericCode { get; set; }
    public string NameAr { get; set; } = default!;
    public string? NameEn { get; set; }
    /// <summary>رمز العرض (مثل $ أو €)</summary>
    public string? Symbol { get; set; }
    /// <summary>عدد الخانات العشرية (افتراضي 2)</summary>
    public int DecimalPlaces { get; set; } = 2;
    /// <summary>متاحة للاستخدام في النظام</summary>
    public bool IsEnabled { get; set; }
    /// <summary>العملة الرئيسية - واحدة فقط</summary>
    public bool IsBase { get; set; }
    /// <summary>ترتيب العرض</summary>
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
