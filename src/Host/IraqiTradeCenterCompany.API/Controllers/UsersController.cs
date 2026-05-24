using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AuthDbContext _db;
    private readonly IPermissionService _permissions;

    public UsersController(AuthDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    // ────────────────────────────────────────────────────────────
    //  قائمة + قراءة
    // ────────────────────────────────────────────────────────────
    [HttpGet]
    [RequirePermission(PermissionRegistry.System.Users.Read)]
    public async Task<IActionResult> List([FromQuery] string? search, CancellationToken ct)
    {
        var q = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u => u.FullName.Contains(s) || u.Phone.Contains(s));
        }

        var users = await q
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id, u.FullName, u.Phone, u.IsActive, u.CreatedAt,
                roles = _db.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.NameAr)
                    .ToList(),
                cashBoxCount = _db.UserCashBoxes.Count(c => c.UserId == u.Id),
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = users });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionRegistry.System.Users.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound(new { success = false, errors = new[] { "المستخدم غير موجود" } });

        var roles = await _db.UserRoles
            .Where(ur => ur.UserId == id)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

        var overrides = await _db.UserPermissionOverrides
            .Where(o => o.UserId == id)
            .Select(o => new { o.PermissionCode, o.IsGranted })
            .ToListAsync(ct);

        var cashBoxes = await _db.UserCashBoxes
            .Where(c => c.UserId == id)
            .Select(c => new { c.CashBoxId, c.CanReceive, c.CanPay })
            .ToListAsync(ct);

        var effective = await _permissions.GetUserPermissionsAsync(id, ct);
        var isSuper   = await _permissions.IsSuperAdminAsync(id, ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                user.Id, user.FullName, user.Phone, user.IsActive, user.CreatedAt,
                roleIds = roles,
                overrides,
                cashBoxes,
                effectivePermissions = effective.ToArray(),
                isSuperAdmin = isSuper,
            }
        });
    }

    // ────────────────────────────────────────────────────────────
    //  إنشاء + تعديل + حذف
    // ────────────────────────────────────────────────────────────
    [HttpPost]
    [RequirePermission(PermissionRegistry.System.Users.Create)]
    public async Task<IActionResult> Create([FromBody] UserCreateDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Phone) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { success = false, errors = new[] { "الاسم ورقم الهاتف وكلمة المرور مطلوبة" } });

        if (await _db.Users.AnyAsync(u => u.Phone == dto.Phone, ct))
            return Conflict(new { success = false, errors = new[] { "رقم الهاتف مستخدم لمستخدم آخر" } });

        var user = new CompanyUser
        {
            Id           = Guid.NewGuid(),
            FullName     = dto.FullName.Trim(),
            Phone        = dto.Phone.Trim(),
            PasswordHash = PasswordHelper.Hash(dto.Password),
            Role         = "User", // legacy column؛ السلطة الحقيقية من الأدوار الجدد
            IsActive     = dto.IsActive ?? true,
            CreatedAt    = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        if (dto.RoleIds is { Count: > 0 })
        {
            foreach (var rid in dto.RoleIds.Distinct())
                _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = rid });
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { success = true, data = new { user.Id } });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionRegistry.System.Users.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UserUpdateDto dto, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound(new { success = false, errors = new[] { "المستخدم غير موجود" } });

        if (!string.IsNullOrWhiteSpace(dto.FullName)) user.FullName = dto.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Phone))
        {
            var phone = dto.Phone.Trim();
            if (await _db.Users.AnyAsync(u => u.Phone == phone && u.Id != id, ct))
                return Conflict(new { success = false, errors = new[] { "رقم الهاتف مستخدم لمستخدم آخر" } });
            user.Phone = phone;
        }
        if (!string.IsNullOrWhiteSpace(dto.Password))
            user.PasswordHash = PasswordHelper.Hash(dto.Password);
        if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;

        await _db.SaveChangesAsync(ct);
        _permissions.InvalidateUser(id);
        return Ok(new { success = true });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionRegistry.System.Users.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound(new { success = false, errors = new[] { "المستخدم غير موجود" } });

        var callerId = GetUserId();
        if (callerId == id)
            return BadRequest(new { success = false, errors = new[] { "لا يمكن حذف حسابك أثناء استخدامه" } });

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        _permissions.InvalidateUser(id);
        return Ok(new { success = true });
    }

    // ────────────────────────────────────────────────────────────
    //  إدارة الأدوار للمستخدم
    // ────────────────────────────────────────────────────────────
    [HttpPut("{id:guid}/roles")]
    [RequirePermission(PermissionRegistry.System.Users.Update)]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] SetRolesDto dto, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == id, ct))
            return NotFound(new { success = false, errors = new[] { "المستخدم غير موجود" } });

        var current = await _db.UserRoles.Where(ur => ur.UserId == id).ToListAsync(ct);
        _db.UserRoles.RemoveRange(current);

        var validRoleIds = await _db.Roles.Select(r => r.Id).ToListAsync(ct);
        var validSet = validRoleIds.ToHashSet();
        foreach (var rid in (dto.RoleIds ?? new List<int>()).Distinct())
        {
            if (validSet.Contains(rid))
                _db.UserRoles.Add(new UserRole { UserId = id, RoleId = rid });
        }
        await _db.SaveChangesAsync(ct);
        _permissions.InvalidateUser(id);
        return Ok(new { success = true });
    }

    // ────────────────────────────────────────────────────────────
    //  إدارة الـ Permission Overrides للمستخدم
    // ────────────────────────────────────────────────────────────
    [HttpPut("{id:guid}/permission-overrides")]
    [RequirePermission(PermissionRegistry.System.Users.Update)]
    public async Task<IActionResult> SetOverrides(Guid id, [FromBody] SetOverridesDto dto, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == id, ct))
            return NotFound(new { success = false, errors = new[] { "المستخدم غير موجود" } });

        var current = await _db.UserPermissionOverrides.Where(o => o.UserId == id).ToListAsync(ct);
        _db.UserPermissionOverrides.RemoveRange(current);

        var validPerms = await _db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var validSet = validPerms.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var o in dto.Overrides ?? Array.Empty<OverrideEntryDto>())
        {
            if (!string.IsNullOrWhiteSpace(o.PermissionCode) && validSet.Contains(o.PermissionCode))
            {
                _db.UserPermissionOverrides.Add(new UserPermissionOverride
                {
                    UserId         = id,
                    PermissionCode = o.PermissionCode,
                    IsGranted      = o.IsGranted,
                });
            }
        }
        await _db.SaveChangesAsync(ct);
        _permissions.InvalidateUser(id);
        return Ok(new { success = true });
    }

    // ────────────────────────────────────────────────────────────
    //  إدارة الصناديق المربوطة بالمستخدم
    // ────────────────────────────────────────────────────────────
    [HttpPut("{id:guid}/cash-boxes")]
    [RequirePermission(PermissionRegistry.System.Users.Update)]
    public async Task<IActionResult> SetCashBoxes(Guid id, [FromBody] SetCashBoxesDto dto, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == id, ct))
            return NotFound(new { success = false, errors = new[] { "المستخدم غير موجود" } });

        var current = await _db.UserCashBoxes.Where(c => c.UserId == id).ToListAsync(ct);
        _db.UserCashBoxes.RemoveRange(current);

        foreach (var c in dto.CashBoxes ?? Array.Empty<UserCashBoxEntryDto>())
        {
            _db.UserCashBoxes.Add(new UserCashBox
            {
                UserId     = id,
                CashBoxId  = c.CashBoxId,
                CanReceive = c.CanReceive,
                CanPay     = c.CanPay,
                AssignedAt = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync(ct);
        _permissions.InvalidateUser(id);
        return Ok(new { success = true });
    }

    // ────────────────────────────────────────────────────────────
    //  /me — يقرأها الفرونت بعد كل reload ليُحدِّث الـ permissions
    // ────────────────────────────────────────────────────────────
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var uid = GetUserId();
        if (uid == Guid.Empty) return Unauthorized();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid, ct);
        if (user is null) return Unauthorized();

        var perms   = await _permissions.GetUserPermissionsAsync(uid, ct);
        var isSuper = await _permissions.IsSuperAdminAsync(uid, ct);
        var cbIds   = await _permissions.GetUserCashBoxIdsAsync(uid, ct);
        var roles   = await _db.UserRoles
            .Where(ur => ur.UserId == uid)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Code)
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                user.Id, user.FullName, user.Phone, user.IsActive,
                roles,
                permissions  = perms.ToArray(),
                cashBoxIds   = cbIds.ToArray(),
                isSuperAdmin = isSuper,
            }
        });
    }

    private Guid GetUserId()
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(idStr, out var g) ? g : Guid.Empty;
    }
}

public record UserCreateDto(string FullName, string Phone, string Password, bool? IsActive, IList<int>? RoleIds);
public record UserUpdateDto(string? FullName, string? Phone, string? Password, bool? IsActive);
public record SetRolesDto(IList<int>? RoleIds);
public record OverrideEntryDto(string PermissionCode, bool IsGranted);
public record SetOverridesDto(IList<OverrideEntryDto>? Overrides);
public record UserCashBoxEntryDto(int CashBoxId, bool CanReceive, bool CanPay);
public record SetCashBoxesDto(IList<UserCashBoxEntryDto>? CashBoxes);
