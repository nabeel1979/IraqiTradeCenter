namespace IraqiTradeCenterCompany.API.Auth.Permissions;

/// <summary>ربط M:N بين <see cref="CompanyUser"/> و <see cref="Role"/>.</summary>
public class UserRole
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }

    public CompanyUser? User { get; set; }
    public Role? Role { get; set; }
}
