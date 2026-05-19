namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;

/// <summary>حالة نشرة أسعار العملات</summary>
public enum CurrencyRateBulletinStatus
{
    /// <summary>مسودّة - قابلة للتعديل، غير معتمدة</summary>
    Draft = 1,
    /// <summary>منشورة - معتمدة، تُستخدم في القيود</summary>
    Published = 2,
    /// <summary>مؤرشفة - غير قابلة للاستخدام</summary>
    Archived = 3
}

/// <summary>
/// نوع العملية لتحويل العملة الأجنبية إلى العملة الرئيسية.
/// Multiply: BaseAmount = ForeignAmount * Rate
/// Divide:   BaseAmount = ForeignAmount / Rate
/// </summary>
public enum CurrencyRateOperation
{
    Multiply = 1,
    Divide = 2
}
