using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Controllers;

[ApiController]
[Authorize]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly AuthDbContext _db;
    private readonly IPermissionService _permissions;

    public RolesController(AuthDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    [HttpGet]
    [RequirePermission(PermissionRegistry.System.Roles.Read)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .OrderByDescending(r => r.IsSuperAdmin)
            .ThenBy(r => r.IsSystemRole ? 0 : 1)
            .ThenBy(r => r.NameAr)
            .Select(r => new
            {
                r.Id,
                r.Code,
                r.NameAr,
                r.Description,
                r.IsSystemRole,
                r.IsSuperAdmin,
                r.IsActive,
                r.CreatedAt,
                permissionCount = r.Permissions.Count,
                userCount       = r.Users.Count,
            })
            .ToListAsync(ct);
        return Ok(new { success = true, data = roles });
    }

    [HttpGet("{id:int}")]
    [RequirePermission(PermissionRegistry.System.Roles.Read)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var role = await _db.Roles
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id, r.Code, r.NameAr, r.Description,
                r.IsSystemRole, r.IsSuperAdmin, r.IsActive, r.CreatedAt,
                permissions = r.Permissions.Select(p => p.PermissionCode).ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (role is null) return NotFound(new { success = false, errors = new[] { "الدور غير موجود" } });
        return Ok(new { success = true, data = role });
    }

    [HttpPost]
    [RequirePermission(PermissionRegistry.System.Roles.Create)]
    public async Task<IActionResult> Create([FromBody] RoleUpsertDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.NameAr))
            return BadRequest(new { success = false, errors = new[] { "كود الدور واسمه مطلوبان" } });

        if (await _db.Roles.AnyAsync(r => r.Code == dto.Code, ct))
            return Conflict(new { success = false, errors = new[] { "هذا الكود مستخدم لدور آخر" } });

        var role = new Role
        {
            Code         = dto.Code.Trim(),
            NameAr       = dto.NameAr.Trim(),
            Description  = dto.Description?.Trim(),
            IsSystemRole = false,
            IsSuperAdmin = false,
            IsActive     = dto.IsActive ?? true,
        };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);

        await ReplacePermissionsAsync(role.Id, dto.Permissions ?? Array.Empty<string>(), ct);

        return Ok(new { success = true, data = new { role.Id } });
    }

    [HttpPut("{id:int}")]
    [RequirePermission(PermissionRegistry.System.Roles.Update)]
    public async Task<IActionResult> Update(int id, [FromBody] RoleUpsertDto dto, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null) return NotFound(new { success = false, errors = new[] { "الدور غير موجود" } });

        // لا نسمح بتغيير الكود أو حالة النظام للأدوار الافتراضية
        if (!role.IsSystemRole && !string.IsNullOrWhiteSpace(dto.Code))
            role.Code = dto.Code.Trim();
        if (!string.IsNullOrWhiteSpace(dto.NameAr))
            role.NameAr = dto.NameAr.Trim();
        role.Description = dto.Description?.Trim();
        if (dto.IsActive.HasValue) role.IsActive = dto.IsActive.Value;

        await _db.SaveChangesAsync(ct);

        // SuperAdmin لا تُعدَّل صلاحياته (محسوبة تلقائياً)
        if (!role.IsSuperAdmin && dto.Permissions is not null)
            await ReplacePermissionsAsync(role.Id, dto.Permissions, ct);

        // أبطل الـ cache لكل المستخدمين الذين يحملون هذا الدور
        var affected = await _db.UserRoles.Where(ur => ur.RoleId == role.Id).Select(ur => ur.UserId).ToListAsync(ct);
        foreach (var uid in affected) _permissions.InvalidateUser(uid);

        return Ok(new { success = true });
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionRegistry.System.Roles.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null) return NotFound(new { success = false, errors = new[] { "الدور غير موجود" } });
        if (role.IsSystemRole)
            return BadRequest(new { success = false, errors = new[] { "لا يمكن حذف أدوار النظام الافتراضية" } });

        var affected = await _db.UserRoles.Where(ur => ur.RoleId == role.Id).Select(ur => ur.UserId).ToListAsync(ct);

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(ct);

        foreach (var uid in affected) _permissions.InvalidateUser(uid);
        return Ok(new { success = true });
    }

    private async Task ReplacePermissionsAsync(int roleId, IEnumerable<string> codes, CancellationToken ct)
    {
        var current = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        _db.RolePermissions.RemoveRange(current);

        var valid = await _db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var validSet = valid.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var code in codes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (validSet.Contains(code))
                _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionCode = code });
        }
        await _db.SaveChangesAsync(ct);
    }
}

public record RoleUpsertDto(string? Code, string? NameAr, string? Description, bool? IsActive, IList<string>? Permissions);
