using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Auth.Permissions;

/// <summary>
/// يُشغَّل مرة عند بدء التطبيق:
///   • يقرأ <see cref="PermissionRegistry"/> ويُدخل أي صلاحية جديدة لـ جدول auth.Permissions.
///   • يُحدِّث الأسماء العربية وترتيب العرض للصلاحيات الموجودة.
///   • يحذف من الجدول أي صلاحية لم تعد في الكود (نظافة).
///
/// النتيجة: جدول الصلاحيات يبقى مطابقاً للكود تماماً بدون migrations يدوية.
/// </summary>
public static class PermissionsBootstrapper
{
    /// <summary>
    /// يُزامن جدول الصلاحيات مع المصدر الموحَّد:
    /// PermissionRegistry (الصلاحيات الثابتة) + <paramref name="additional"/> (الديناميكية، مثل صلاحيات أنواع السندات).
    /// </summary>
    public static async Task SyncAsync(
        AuthDbContext db,
        IEnumerable<Permission>? additional = null,
        CancellationToken ct = default)
    {
        // ‎ندمج الـ static مع الـ dynamic، مع منع التكرار بكود الصلاحية.
        // ‎عند التكرار نُفضّل القادم من Registry (وهو الذي يُفترض أنه نهائي).
        var dynamicList = additional?.ToList() ?? new List<Permission>();
        var staticList  = PermissionRegistry.GetAll().ToList();
        var fromCode    = new List<Permission>(staticList.Count + dynamicList.Count);
        var seen        = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in staticList.Concat(dynamicList))
        {
            if (seen.Add(p.Code)) fromCode.Add(p);
        }
        var fromDb = await db.Permissions.ToListAsync(ct);

        var codeSet = fromCode.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dbMap   = fromDb.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

        // إضافة الجديدة
        foreach (var p in fromCode)
        {
            if (!dbMap.TryGetValue(p.Code, out var existing))
            {
                db.Permissions.Add(p);
            }
            else
            {
                existing.NameAr       = p.NameAr;
                existing.Module       = p.Module;
                existing.Resource     = p.Resource;
                existing.Action       = p.Action;
                existing.Description  = p.Description;
                existing.DisplayOrder = p.DisplayOrder;
            }
        }

        // حذف ما لم يعد موجوداً في الكود (مع تنظيف الـ RolePermissions/Overrides المرتبطة)
        var toDelete = fromDb.Where(p => !codeSet.Contains(p.Code)).ToList();
        if (toDelete.Count > 0)
        {
            var deletedCodes = toDelete.Select(p => p.Code).ToList();
            var rps = await db.RolePermissions.Where(rp => deletedCodes.Contains(rp.PermissionCode)).ToListAsync(ct);
            var ovs = await db.UserPermissionOverrides.Where(o => deletedCodes.Contains(o.PermissionCode)).ToListAsync(ct);
            db.RolePermissions.RemoveRange(rps);
            db.UserPermissionOverrides.RemoveRange(ovs);
            db.Permissions.RemoveRange(toDelete);
        }

        await db.SaveChangesAsync(ct);
    }
}
