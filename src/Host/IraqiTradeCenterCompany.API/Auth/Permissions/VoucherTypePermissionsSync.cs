using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Auth.Permissions;

/// <summary>
/// يُزامن جدول <c>auth.Permissions</c> مع أنواع السندات المعرَّفة في
/// <c>acc.JournalVoucherTypes</c>: لكل نوع نُولّد 6 صلاحيات (Read/Create/Update/
/// Delete/Print/Post). يُستدعى عند بدء التطبيق وبعد كل تعديل على أنواع السندات.
///
/// كما يُنفّذ مرة واحدة هجرة قائمة الصلاحيات القديمة (Accounting.Vouchers.{Action})
/// إلى الديناميكية الجديدة (Accounting.Vouchers.{CODE}.{Action}) بحيث لا يفقد
/// أي دور صلاحياته بعد الترقية.
/// </summary>
public interface IVoucherTypePermissionsSync
{
    Task SyncAsync(CancellationToken ct = default);
}

public class VoucherTypePermissionsSync : IVoucherTypePermissionsSync
{
    private readonly AuthDbContext _auth;
    private readonly IAccountingDbContext _accounting;
    private readonly ILogger<VoucherTypePermissionsSync> _log;

    private static readonly string[] AllActions =
    {
        PermissionRegistry.Actions.Read,
        PermissionRegistry.Actions.Create,
        PermissionRegistry.Actions.Update,
        PermissionRegistry.Actions.Delete,
        PermissionRegistry.Actions.Print,
        PermissionRegistry.Actions.Post,
    };

    public VoucherTypePermissionsSync(
        AuthDbContext auth,
        IAccountingDbContext accounting,
        ILogger<VoucherTypePermissionsSync> log)
    {
        _auth = auth;
        _accounting = accounting;
        _log = log;
    }

    public async Task SyncAsync(CancellationToken ct = default)
    {
        // ‎نقرأ كل أنواع السندات (المُفعَّلة + الموقوفة) — حتى الـ Disabled يحتفظ
        // ‎بصلاحياته للتاريخية. الأنواع المحذوفة (IsDeleted) مستثناة عبر Query Filter.
        var types = await _accounting.JournalVoucherTypes
            .AsNoTracking()
            .Select(t => new VoucherTypePermissionFactory.VoucherTypeRef(t.Code, t.NameAr, t.DisplayOrder))
            .ToListAsync(ct);

        var dynamicPerms = VoucherTypePermissionFactory.Build(types).ToList();

        // 1) أدخل الصلاحيات الديناميكية في الجدول (بدون حذف). هذا ضروري قبل الـ
        //    migration التالي لأن RolePermission لها FK إلى Permission.Code.
        await EnsurePermissionsExistAsync(dynamicPerms, ct);

        // 2) Migration لمرة واحدة: انسخ صلاحيات Accounting.Vouchers.{Action}
        //    القديمة لكل دور إلى نسخ ديناميكية لكل voucher type. تُنفَّذ كل مرة
        //    لكنها idempotent (لا تُكرر إن وُجدت الديناميكية مسبقاً).
        await MigrateLegacyRolePermissionsAsync(types, ct);

        // 3) Bootstrap كامل: يحذف القديمة (مع RolePermissions المتبقية) ويُحدِّث
        //    الأسماء العربية وترتيب العرض، ويُبقي الديناميكية المُمرَّرة.
        await PermissionsBootstrapper.SyncAsync(_auth, dynamicPerms, ct);

        _log.LogInformation(
            "[VoucherTypePermissionsSync] Synced {Count} voucher type(s) → {PermCount} permission(s).",
            types.Count, dynamicPerms.Count);
    }

    private async Task EnsurePermissionsExistAsync(List<Permission> dynamicPerms, CancellationToken ct)
    {
        if (dynamicPerms.Count == 0) return;

        var existingCodes = await _auth.Permissions.Select(p => p.Code).ToListAsync(ct);
        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var p in dynamicPerms)
        {
            if (!existingSet.Contains(p.Code))
            {
                _auth.Permissions.Add(p);
                added++;
            }
        }

        if (added > 0) await _auth.SaveChangesAsync(ct);
    }

    private async Task MigrateLegacyRolePermissionsAsync(
        List<VoucherTypePermissionFactory.VoucherTypeRef> types,
        CancellationToken ct)
    {
        if (types.Count == 0) return;

        var legacyCodes = AllActions
            .Select(a => $"{PermissionRegistry.Accounting.Vouchers.Prefix}{a}")
            .ToArray();

        var legacyRps = await _auth.RolePermissions
            .Where(rp => legacyCodes.Contains(rp.PermissionCode))
            .ToListAsync(ct);

        if (legacyRps.Count == 0) return;

        // ‎الـ tuples الموجودة بالفعل لتفادي التكرار (constraint على PK مركّب)
        var existing = await _auth.RolePermissions
            .Where(rp => rp.PermissionCode.StartsWith(PermissionRegistry.Accounting.Vouchers.Prefix))
            .Select(rp => new { rp.RoleId, rp.PermissionCode })
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(
            existing.Select(x => $"{x.RoleId}|{x.PermissionCode}"),
            StringComparer.OrdinalIgnoreCase);

        var migrated = 0;
        foreach (var rp in legacyRps)
        {
            // ‎ {Prefix} = "Accounting.Vouchers." → بعدها مباشرة الـ Action
            var action = rp.PermissionCode[PermissionRegistry.Accounting.Vouchers.Prefix.Length..];
            foreach (var t in types)
            {
                var code = t.Code.Trim().ToUpperInvariant();
                var dynCode = $"{PermissionRegistry.Accounting.Vouchers.Prefix}{code}.{action}";
                var key = $"{rp.RoleId}|{dynCode}";
                if (existingSet.Add(key))
                {
                    _auth.RolePermissions.Add(new RolePermission
                    {
                        RoleId = rp.RoleId,
                        PermissionCode = dynCode,
                    });
                    migrated++;
                }
            }
        }

        if (migrated > 0)
        {
            await _auth.SaveChangesAsync(ct);
            _log.LogInformation(
                "[VoucherTypePermissionsSync] Migrated {Count} legacy voucher RolePermissions → dynamic.",
                migrated);
        }
    }
}
