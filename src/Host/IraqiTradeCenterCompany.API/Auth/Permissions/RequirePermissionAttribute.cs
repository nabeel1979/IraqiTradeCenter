using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IraqiTradeCenterCompany.API.Auth.Permissions;

/// <summary>
/// Attribute بسيط للـ endpoints: <c>[RequirePermission("Accounting.JournalEntries.Post")]</c>.
/// يقرأ Claims الـ JWT أولاً (لأداء أعلى)، ثم يرجع لـ DB إذا لم تكن الصلاحيات في الـ Token.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permission;

    public RequirePermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // ‎الـ SuperAdmin يتجاوز كل الفحوصات.
        if (user.IsInRole("SuperAdmin")) return;

        // ‎الصلاحيات مُضمَّنة في الـ JWT كـ claims باسم "perm".
        if (user.HasClaim("perm", _permission)) return;

        // ‎fallback: قراءة من DB (لو الـ token قديم أو نسي إضافتها).
        var userIdStr = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? user.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdStr, out var userId))
        {
            var svc = context.HttpContext.RequestServices.GetService(typeof(IPermissionService)) as IPermissionService;
            if (svc != null && await svc.HasPermissionAsync(userId, _permission)) return;
        }

        context.Result = new ObjectResult(new
        {
            success = false,
            errors  = new[] { $"ليس لديك صلاحية: {_permission}" },
            requiredPermission = _permission,
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
