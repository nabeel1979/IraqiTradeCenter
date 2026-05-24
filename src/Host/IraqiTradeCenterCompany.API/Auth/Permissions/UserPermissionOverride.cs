namespace IraqiTradeCenterCompany.API.Auth.Permissions;

/// <summary>
/// استثناء لمستخدم محدد: يمنح أو يمنع صلاحية بصرف النظر عن أدواره.
/// مفيد عندما تريد أن يكون كل المحاسبين لديهم نفس الصلاحيات ما عدا شخصاً واحداً يحتاج
/// إضافة (Grant) أو إخراج (Deny) صلاحية واحدة دون إنشاء دور جديد له فقط.
///
/// الأولوية في حساب الصلاحيات: SuperAdmin → Deny override → Grant override → Role permissions.
/// </summary>
public class UserPermissionOverride
{
    public Guid UserId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>true = منح صلاحية إضافية، false = حجب صلاحية حتى لو أعطاها الدور.</summary>
    public bool IsGranted { get; set; }

    public CompanyUser? User { get; set; }
    public Permission? Permission { get; set; }
}
