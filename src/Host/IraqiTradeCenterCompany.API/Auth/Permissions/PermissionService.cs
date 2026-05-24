using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IraqiTradeCenterCompany.API.Auth.Permissions;

/// <summary>
/// تنفيذ بكاش بسيط في الذاكرة (IMemoryCache) عمر 5 دقائق. عند تغيير صلاحيات
/// المستخدم تستدعي <see cref="InvalidateUser"/> ليُعاد الحساب من DB المرة القادمة.
/// </summary>
public class PermissionService : IPermissionService
{
    private const int CacheMinutes = 5;
    private const string KeyPrefix = "perms::";
    private const string SuperPrefix = "super::";
    private const string CashBoxPrefix = "cb::";

    private readonly AuthDbContext _db;
    private readonly IMemoryCache _cache;

    public PermissionService(AuthDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<bool> IsSuperAdminAsync(Guid userId, CancellationToken ct = default)
    {
        var key = SuperPrefix + userId;
        if (_cache.TryGetValue<bool>(key, out var cached)) return cached;

        var isSuper = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r)
            .AnyAsync(r => r.IsActive && r.IsSuperAdmin, ct);

        _cache.Set(key, isSuper, TimeSpan.FromMinutes(CacheMinutes));
        return isSuper;
    }

    public async Task<HashSet<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var key = KeyPrefix + userId;
        if (_cache.TryGetValue<HashSet<string>>(key, out var cached) && cached != null) return cached;

        // SuperAdmin → كل الصلاحيات (نقرأها من جدول Permission المسقَّط آلياً من Registry)
        if (await IsSuperAdminAsync(userId, ct))
        {
            var all = await _db.Permissions.AsNoTracking().Select(p => p.Code).ToListAsync(ct);
            var allSet = new HashSet<string>(all, StringComparer.OrdinalIgnoreCase);
            _cache.Set(key, allSet, TimeSpan.FromMinutes(CacheMinutes));
            return allSet;
        }

        // اجمع صلاحيات كل أدواره النشطة
        var rolePerms = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_db.Roles.Where(r => r.IsActive), ur => ur.RoleId, r => r.Id, (ur, r) => r.Id)
            .Join(_db.RolePermissions, rid => rid, rp => rp.RoleId, (rid, rp) => rp.PermissionCode)
            .Distinct()
            .ToListAsync(ct);

        var effective = new HashSet<string>(rolePerms, StringComparer.OrdinalIgnoreCase);

        // طبّق الـ overrides
        var overrides = await _db.UserPermissionOverrides
            .Where(o => o.UserId == userId)
            .Select(o => new { o.PermissionCode, o.IsGranted })
            .ToListAsync(ct);
        foreach (var o in overrides)
        {
            if (o.IsGranted) effective.Add(o.PermissionCode);
            else             effective.Remove(o.PermissionCode);
        }

        _cache.Set(key, effective, TimeSpan.FromMinutes(CacheMinutes));
        return effective;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(permissionCode)) return false;
        var perms = await GetUserPermissionsAsync(userId, ct);
        return perms.Contains(permissionCode);
    }

    public async Task<HashSet<int>> GetUserCashBoxIdsAsync(Guid userId, CancellationToken ct = default)
    {
        var key = CashBoxPrefix + userId;
        if (_cache.TryGetValue<HashSet<int>>(key, out var cached) && cached != null) return cached;

        var ids = await _db.UserCashBoxes
            .Where(uc => uc.UserId == userId)
            .Select(uc => uc.CashBoxId)
            .ToListAsync(ct);

        var set = new HashSet<int>(ids);
        _cache.Set(key, set, TimeSpan.FromMinutes(CacheMinutes));
        return set;
    }

    public void InvalidateUser(Guid userId)
    {
        _cache.Remove(KeyPrefix + userId);
        _cache.Remove(SuperPrefix + userId);
        _cache.Remove(CashBoxPrefix + userId);
    }
}
