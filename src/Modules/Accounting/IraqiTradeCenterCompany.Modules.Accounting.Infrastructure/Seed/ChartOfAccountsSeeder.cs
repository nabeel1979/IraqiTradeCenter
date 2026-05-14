using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Seed;

public static class ChartOfAccountsSeeder
{
    public static async Task SeedAsync(AccountingDbContext db)
    {
        if (await db.Accounts.AnyAsync()) return;

        // المستوى 1
        var assets = Account.Create("1", "الأصول", AccountType.Asset, AccountNature.Debit, null, 1, false);
        var liab = Account.Create("2", "الخصوم", AccountType.Liability, AccountNature.Credit, null, 1, false);
        var equity = Account.Create("3", "حقوق الملكية", AccountType.Equity, AccountNature.Credit, null, 1, false);
        var rev = Account.Create("4", "الإيرادات", AccountType.Revenue, AccountNature.Credit, null, 1, false);
        var exp = Account.Create("5", "المصروفات", AccountType.Expense, AccountNature.Debit, null, 1, false);
        await db.Accounts.AddRangeAsync(assets, liab, equity, rev, exp);
        await db.SaveChangesAsync();

        // المستوى 2
        var ca = Account.Create("1.1", "الأصول المتداولة", AccountType.Asset, AccountNature.Debit, assets.Id, 2, false);
        var ar = Account.Create("1.2", "الذمم المدينة", AccountType.Asset, AccountNature.Debit, assets.Id, 2, false);
        var inv = Account.Create("1.3", "المخزون", AccountType.Asset, AccountNature.Debit, assets.Id, 2, false);
        var fa = Account.Create("1.4", "الأصول الثابتة", AccountType.Asset, AccountNature.Debit, assets.Id, 2, false);
        var cl = Account.Create("2.1", "الخصوم المتداولة", AccountType.Liability, AccountNature.Credit, liab.Id, 2, false);
        var cap = Account.Create("3.1", "رأس المال", AccountType.Equity, AccountNature.Credit, equity.Id, 2, false);
        var oprev = Account.Create("4.1", "إيرادات تشغيلية", AccountType.Revenue, AccountNature.Credit, rev.Id, 2, false);
        var cogs = Account.Create("5.1", "تكلفة البضاعة", AccountType.Expense, AccountNature.Debit, exp.Id, 2, false);
        var sales_exp = Account.Create("5.2", "مصروفات البيع", AccountType.Expense, AccountNature.Debit, exp.Id, 2, false);
        var admin = Account.Create("5.3", "مصروفات إدارية", AccountType.Expense, AccountNature.Debit, exp.Id, 2, false);
        var ops = Account.Create("5.4", "مصروفات تشغيلية", AccountType.Expense, AccountNature.Debit, exp.Id, 2, false);
        await db.Accounts.AddRangeAsync(ca, ar, inv, fa, cl, cap, oprev, cogs, sales_exp, admin, ops);
        await db.SaveChangesAsync();

        // المستوى 3 - الحسابات التفصيلية (Leaf = true)
        var leaves = new[]
        {
            // 1.1 - الأصول المتداولة
            Account.Create("1.1.01", "الصندوق", AccountType.Asset, AccountNature.Debit, ca.Id, 3, true),
            Account.Create("1.1.02", "البنك", AccountType.Asset, AccountNature.Debit, ca.Id, 3, true),
            // 1.2 - الذمم المدينة
            Account.Create("1.2.01", "ذمم العملاء", AccountType.Asset, AccountNature.Debit, ar.Id, 3, true),
            // 1.3 - المخزون
            Account.Create("1.3.01", "مخزون البضاعة", AccountType.Asset, AccountNature.Debit, inv.Id, 3, true),
            // 2.1 - الخصوم المتداولة
            Account.Create("2.1.01", "ذمم الموردين", AccountType.Liability, AccountNature.Credit, cl.Id, 3, true),
            Account.Create("2.1.02", "ضريبة مستحقة الدفع", AccountType.Liability, AccountNature.Credit, cl.Id, 3, true),
            Account.Create("2.1.03", "عمولات مستحقة الدفع", AccountType.Liability, AccountNature.Credit, cl.Id, 3, true),
            Account.Create("2.1.04", "رواتب مستحقة الدفع", AccountType.Liability, AccountNature.Credit, cl.Id, 3, true),
            // 3.1 - رأس المال
            Account.Create("3.1.01", "رأس المال", AccountType.Equity, AccountNature.Credit, cap.Id, 3, true),
            Account.Create("3.1.02", "الأرباح المحتجزة", AccountType.Equity, AccountNature.Credit, cap.Id, 3, true),
            // 4.1 - الإيرادات
            Account.Create("4.1.01", "إيرادات المبيعات", AccountType.Revenue, AccountNature.Credit, oprev.Id, 3, true),
            Account.Create("4.1.02", "خصومات ممنوحة", AccountType.Revenue, AccountNature.Debit, oprev.Id, 3, true),
            Account.Create("4.1.03", "مرتجع المبيعات", AccountType.Revenue, AccountNature.Debit, oprev.Id, 3, true),
            // 5.1 - تكلفة البضاعة
            Account.Create("5.1.01", "تكلفة البضاعة المباعة", AccountType.Expense, AccountNature.Debit, cogs.Id, 3, true),
            // 5.2 - مصروفات البيع
            Account.Create("5.2.01", "مصروف عمولات المندوبين", AccountType.Expense, AccountNature.Debit, sales_exp.Id, 3, true),
            Account.Create("5.2.02", "نقل وشحن", AccountType.Expense, AccountNature.Debit, sales_exp.Id, 3, true),
            // 5.3 - إدارية
            Account.Create("5.3.01", "الرواتب", AccountType.Expense, AccountNature.Debit, admin.Id, 3, true),
            Account.Create("5.3.02", "الإيجار", AccountType.Expense, AccountNature.Debit, admin.Id, 3, true),
            // 5.4 - تشغيلية
            Account.Create("5.4.01", "كهرباء وماء", AccountType.Expense, AccountNature.Debit, ops.Id, 3, true),
            Account.Create("5.4.02", "اتصالات وإنترنت", AccountType.Expense, AccountNature.Debit, ops.Id, 3, true),
        };
        await db.Accounts.AddRangeAsync(leaves);
        await db.SaveChangesAsync();
    }
}
