namespace IraqiTradeCenterCompany.API.Auth.Permissions;

public interface IPermissionService
{
    /// <summary>يحسب الصلاحيات الفعلية لمستخدم: union(role permissions) ± user overrides.</summary>
    Task<HashSet<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>هل هذا المستخدم لديه الصلاحية المحددة (يحترم SuperAdmin override).</summary>
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct = default);

    /// <summary>هل هذا المستخدم SuperAdmin (له كل الصلاحيات تلقائياً).</summary>
    Task<bool> IsSuperAdminAsync(Guid userId, CancellationToken ct = default);

    /// <summary>قائمة معرّفات الصناديق التي يستطيع المستخدم استخدامها (فارغة = لا شيء، إلا إن كان SuperAdmin).</summary>
    Task<HashSet<int>> GetUserCashBoxIdsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>يُلغي أي cache مخزَّن لمستخدم — استدعِه عند تغيير أدواره أو override.</summary>
    void InvalidateUser(Guid userId);
}
