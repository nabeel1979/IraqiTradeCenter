using System.Security.Claims;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;

namespace IraqiTradeCenterCompany.API.Extensions;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;
    private readonly IServiceProvider _services;

    public CurrentUserService(IHttpContextAccessor http, IServiceProvider services)
    {
        _http = http;
        _services = services;
    }

    private ClaimsPrincipal? User => _http.HttpContext?.User;
    public Guid? UserId
    {
        get
        {
            var v = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(v, out var g) ? g : null;
        }
    }
    public string? FullName => User?.FindFirst(ClaimTypes.Name)?.Value;
    public int CompanyId
    {
        get
        {
            var v = User?.FindFirst("companyId")?.Value;
            return int.TryParse(v, out var i) ? i : 0;
        }
    }
    public int? SalesRepId
    {
        get
        {
            var v = User?.FindFirst("salesRepId")?.Value;
            return int.TryParse(v, out var i) ? i : null;
        }
    }
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsSuperAdmin => User?.IsInRole("SuperAdmin") ?? false;

    public bool HasPermission(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode)) return false;
        if (IsSuperAdmin) return true;

        // مسار سريع: قراءة من claims (لا I/O)
        if (User?.HasClaim("perm", permissionCode) == true) return true;

        // fallback: قراءة من DB إن لم تكن في الـ token (token قديم/incomplete)
        var uid = UserId;
        if (uid is null) return false;
        var svc = _services.GetService(typeof(IPermissionService)) as IPermissionService;
        if (svc is null) return false;
        return svc.HasPermissionAsync(uid.Value, permissionCode).GetAwaiter().GetResult();
    }
}
