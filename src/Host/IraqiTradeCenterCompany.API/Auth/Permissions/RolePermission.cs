namespace IraqiTradeCenterCompany.API.Auth.Permissions;

/// <summary>ربط M:N بين <see cref="Role"/> و <see cref="Permission"/>.</summary>
public class RolePermission
{
    public int RoleId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;

    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}
