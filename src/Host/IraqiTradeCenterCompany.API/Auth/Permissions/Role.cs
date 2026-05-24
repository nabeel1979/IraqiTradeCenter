namespace IraqiTradeCenterCompany.API.Auth.Permissions;

/// <summary>
/// مجموعة صلاحيات قابلة للإسناد لعدة مستخدمين. الأدوار الافتراضية (SuperAdmin، Accountant، Cashier...)
/// تُولَّد من الـ Seeder بـ <see cref="IsSystemRole"/> = true ولا يمكن حذفها.
/// </summary>
public class Role
{
    public int Id { get; set; }

    /// <summary>كود مختصر فريد (SuperAdmin / Accountant / Cashier / Viewer...). يستخدم برمجياً.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>الاسم العربي للعرض.</summary>
    public string NameAr { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>true لأدوار النظام الافتراضية — لا يمكن حذفها أو تعديل كودها.</summary>
    public bool IsSystemRole { get; set; }

    /// <summary>true = يمنح كل صلاحيات النظام تلقائياً (لا حاجة لربط صلاحيات بهذا الدور).</summary>
    public bool IsSuperAdmin { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> Users { get; set; } = new List<UserRole>();
}
