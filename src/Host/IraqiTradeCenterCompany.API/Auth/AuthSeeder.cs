using IraqiTradeCenterCompany.API.Auth.Permissions;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Auth;

public static class AuthSeeder
{
    /// <param name="voucherTypeCodes">
    /// أكواد أنواع السندات الموجودة (مثل RV/PV/AV) — تُستخدم لإنشاء الصلاحيات
    /// الديناميكية للأدوار الافتراضية. لو فارغة تتخطّى صلاحيات السندات.
    /// </param>
    public static async Task SeedAsync(AuthDbContext db, IConfiguration config, IEnumerable<string>? voucherTypeCodes = null)
    {
        var codes = (voucherTypeCodes ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        // 1) الأدوار الافتراضية — تُنشأ مرة واحدة وتُحدَّث وصفها فقط
        await SeedDefaultRolesAsync(db, codes);

        // 2) المستخدم الأول
        if (!await db.Users.AnyAsync())
        {
            var phone = config["InitialAdmin:Phone"] ?? "admin";
            var name  = config["InitialAdmin:FullName"] ?? "المدير العام";
            var pass  = config["InitialAdmin:Password"] ?? "Admin@2024";

            var admin = new CompanyUser
            {
                Id           = Guid.NewGuid(),
                FullName     = name,
                Phone        = phone,
                PasswordHash = PasswordHelper.Hash(pass),
                Role         = "SuperAdmin",
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            // اربطه بدور SuperAdmin
            var superRole = await db.Roles.FirstAsync(r => r.Code == "SuperAdmin");
            db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = superRole.Id });
            await db.SaveChangesAsync();
        }
        else
        {
            // قد يكون النظام مُرَقَّى من إصدار قديم — اربط أي مستخدم بدون أدوار بدوره النصي القديم
            await BackfillExistingUsersAsync(db);
        }
    }

    private static async Task SeedDefaultRolesAsync(AuthDbContext db, List<string> voucherTypeCodes)
    {
        var defaults = new[]
        {
            new Role { Code = "SuperAdmin", NameAr = "مدير النظام (كل الصلاحيات)", Description = "وصول كامل لكل شيء بدون استثناء.", IsSystemRole = true, IsSuperAdmin = true },
            new Role { Code = "Admin",      NameAr = "مدير عام",                  Description = "صلاحيات إدارية واسعة بدون امتيازات الـ SuperAdmin.", IsSystemRole = true },
            new Role { Code = "Accountant", NameAr = "محاسب",                     Description = "إدخال وتعديل القيود والسندات.", IsSystemRole = true },
            new Role { Code = "Cashier",    NameAr = "أمين صندوق",                Description = "تنفيذ سندات القبض والدفع على الصناديق المسموحة.", IsSystemRole = true },
            new Role { Code = "Viewer",     NameAr = "مشاهد فقط",                 Description = "قراءة التقارير دون أي تعديل.", IsSystemRole = true },
        };

        foreach (var r in defaults)
        {
            var existing = await db.Roles.FirstOrDefaultAsync(x => x.Code == r.Code);
            if (existing is null)
            {
                db.Roles.Add(r);
            }
            else
            {
                existing.NameAr       = r.NameAr;
                existing.Description  = r.Description;
                existing.IsSystemRole = true;
                if (r.IsSuperAdmin) existing.IsSuperAdmin = true;
            }
        }
        await db.SaveChangesAsync();

        // أعطِ الأدوار غير SuperAdmin مجموعات صلاحيات منطقية (إن لم تكن مضبوطة بعد)
        await EnsureRoleHasPermissionsAsync(db, "Admin", DefaultAdminPermissions(voucherTypeCodes));
        await EnsureRoleHasPermissionsAsync(db, "Accountant", DefaultAccountantPermissions(voucherTypeCodes));
        await EnsureRoleHasPermissionsAsync(db, "Cashier", DefaultCashierPermissions(voucherTypeCodes));
        await EnsureRoleHasPermissionsAsync(db, "Viewer", DefaultViewerPermissions(voucherTypeCodes));
    }

    private static async Task EnsureRoleHasPermissionsAsync(AuthDbContext db, string roleCode, IEnumerable<string> codes)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == roleCode);
        if (role is null) return;
        if (await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id)) return; // المسؤول قد يكون عدّلها — لا نلمسها

        // أضف فقط ما هو موجود في جدول Permissions (الذي يُسقَّط من الـ Registry)
        var validCodes = await db.Permissions.Select(p => p.Code).ToListAsync();
        var validSet = validCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var code in codes.Distinct())
        {
            if (validSet.Contains(code))
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = code });
        }
        await db.SaveChangesAsync();
    }

    private static async Task BackfillExistingUsersAsync(AuthDbContext db)
    {
        var users = await db.Users.ToListAsync();
        var roles = await db.Roles.ToDictionaryAsync(r => r.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var u in users)
        {
            if (await db.UserRoles.AnyAsync(ur => ur.UserId == u.Id)) continue;
            if (roles.TryGetValue(u.Role, out var r))
                db.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = r.Id });
            else if (roles.TryGetValue("Viewer", out var v))
                db.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = v.Id });
        }
        await db.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────
    //  حِزَم الصلاحيات الافتراضية لكل دور (يمكن تعديلها لاحقاً من الواجهة)
    // ────────────────────────────────────────────────────────────
    private static IEnumerable<string> DefaultAdminPermissions(List<string> voucherCodes)
    {
        foreach (var code in PermissionRegistry.GetAll().Select(p => p.Code))
            yield return code;
        // ‎صلاحيات السندات الديناميكية: Admin يحصل على كل الإجراءات لكل نوع
        foreach (var v in voucherCodes)
        {
            yield return PermissionRegistry.Accounting.Vouchers.Read(v);
            yield return PermissionRegistry.Accounting.Vouchers.Create(v);
            yield return PermissionRegistry.Accounting.Vouchers.Update(v);
            yield return PermissionRegistry.Accounting.Vouchers.Delete(v);
            yield return PermissionRegistry.Accounting.Vouchers.Print(v);
            yield return PermissionRegistry.Accounting.Vouchers.Post(v);
        }
    }

    private static IEnumerable<string> DefaultAccountantPermissions(List<string> voucherCodes)
    {
        var fixedPerms = new[]
        {
            PermissionRegistry.Accounting.JournalEntries.Read,
            PermissionRegistry.Accounting.JournalEntries.Create,
            PermissionRegistry.Accounting.JournalEntries.Update,
            PermissionRegistry.Accounting.JournalEntries.Print,
            PermissionRegistry.Accounting.JournalEntries.Post,
            PermissionRegistry.Accounting.Accounts.Read,
            PermissionRegistry.Accounting.Accounts.Create,
            PermissionRegistry.Accounting.Accounts.Update,
            PermissionRegistry.Accounting.TrialBalance.Read,
            PermissionRegistry.Accounting.TrialBalance.Print,
            PermissionRegistry.Accounting.TrialBalance.Export,
            PermissionRegistry.Accounting.AccountStatement.Read,
            PermissionRegistry.Accounting.AccountStatement.Print,
            PermissionRegistry.Accounting.AccountStatement.Export,
            PermissionRegistry.Accounting.CashBoxes.Read,
            PermissionRegistry.Accounting.CashBoxes.ViewAll,
            PermissionRegistry.Accounting.FiscalYears.Read,
            PermissionRegistry.Accounting.CurrencyRates.Read,
            PermissionRegistry.Accounting.VoucherTypes.Read,
        };
        foreach (var p in fixedPerms) yield return p;
        foreach (var v in voucherCodes)
        {
            yield return PermissionRegistry.Accounting.Vouchers.Read(v);
            yield return PermissionRegistry.Accounting.Vouchers.Create(v);
            yield return PermissionRegistry.Accounting.Vouchers.Update(v);
            yield return PermissionRegistry.Accounting.Vouchers.Print(v);
            yield return PermissionRegistry.Accounting.Vouchers.Post(v);
        }
    }

    private static IEnumerable<string> DefaultCashierPermissions(List<string> voucherCodes)
    {
        yield return PermissionRegistry.Accounting.Accounts.Read;
        yield return PermissionRegistry.Accounting.CashBoxes.Read;
        // أمين الصندوق يقرأ/يُنشئ/يطبع/يرحّل كل أنواع السندات
        foreach (var v in voucherCodes)
        {
            yield return PermissionRegistry.Accounting.Vouchers.Read(v);
            yield return PermissionRegistry.Accounting.Vouchers.Create(v);
            yield return PermissionRegistry.Accounting.Vouchers.Print(v);
            yield return PermissionRegistry.Accounting.Vouchers.Post(v);
        }
    }

    private static IEnumerable<string> DefaultViewerPermissions(List<string> voucherCodes)
    {
        yield return PermissionRegistry.Accounting.JournalEntries.Read;
        yield return PermissionRegistry.Accounting.Accounts.Read;
        yield return PermissionRegistry.Accounting.TrialBalance.Read;
        yield return PermissionRegistry.Accounting.AccountStatement.Read;
        yield return PermissionRegistry.Accounting.CashBoxes.Read;
        // المشاهد يقرأ كل أنواع السندات
        foreach (var v in voucherCodes)
        {
            yield return PermissionRegistry.Accounting.Vouchers.Read(v);
        }
    }
}
