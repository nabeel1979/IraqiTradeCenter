namespace IraqiTradeCenterCompany.API.Settings;

/// <summary>
/// إعدادات الشركة - تُستخدم في الطباعة وعرض الهوية
/// </summary>
public class CompanySettings
{
    public int Id { get; set; } = 1; // singleton
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? TaxNumber { get; set; }
    public string? Currency { get; set; }
    /// <summary>
    /// أسعار تحويل مقارنة بالعملة الأساسية (Currency): كل وحدة من العملة الأجنبية = N من العملة الأساسية.
    /// مثال عند الأساس IQD: {"USD":1320,"EUR":1420}
    /// </summary>
    public string? ExchangeRatesJson { get; set; }
    /// <summary>اللوكو كـ data URI (data:image/png;base64,...) أو URL</summary>
    public string? LogoBase64 { get; set; }
    /// <summary>عنوان رأس الطباعة المخصص (إن لم يُحدَّد يُستخدم اسم الشركة)</summary>
    public string? PrintHeader { get; set; }
    /// <summary>تذييل الطباعة المخصص</summary>
    public string? PrintFooter { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
