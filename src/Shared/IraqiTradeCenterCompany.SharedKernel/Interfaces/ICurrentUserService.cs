namespace IraqiTradeCenterCompany.SharedKernel.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? FullName { get; }
    int CompanyId { get; }
    int? SalesRepId { get; }
    bool IsAuthenticated { get; }

    /// <summary>true إذا كان المستخدم الحالي يحمل دور SuperAdmin (أو لم يكن مسجَّلاً — للسيناريوهات الخلفية).</summary>
    bool IsSuperAdmin { get; }

    /// <summary>
    /// يفحص صلاحية مفردة بقراءة JWT أولاً ثم DB كـ fallback.
    /// تستعمله مكوّنات الـ Application لاتخاذ قرارات منطقية (مثلاً: auto-post أم Draft).
    /// </summary>
    bool HasPermission(string permissionCode);
}
