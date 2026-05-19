namespace IraqiTradeCenterCompany.API.Auth;

public class CompanyUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>JSON صغيرة لتخزين تفضيلات المستخدم (طي/إخفاء الأقسام، تفضيلات الواجهة...). NULL = لم يضبطها بعد.</summary>
    public string? Preferences { get; set; }
}
