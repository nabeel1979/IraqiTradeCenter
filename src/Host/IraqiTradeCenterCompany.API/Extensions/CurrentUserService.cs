using System.Security.Claims;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;

namespace IraqiTradeCenterCompany.API.Extensions;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;
    public CurrentUserService(IHttpContextAccessor http) => _http = http;

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
}
