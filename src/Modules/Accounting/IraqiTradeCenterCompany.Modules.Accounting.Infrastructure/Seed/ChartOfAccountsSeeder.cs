using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Seed;

/// <summary>
/// شجرة الحسابات وفق النظام المحاسبي الموحد العراقي (UCAS - Unified Chart of Accounts System).
/// المجموعات الرئيسية:
///   1 الأصول | 2 الخصوم | 3 حقوق الملكية | 4 الإيرادات | 5 المصروفات
/// تنسيق الترقيم: G.SS.XX  (مجموعة . مستوى2 . مستوى3 تفصيلي)
/// </summary>
public static class ChartOfAccountsSeeder
{
    public static async Task SeedAsync(AccountingDbContext db)
    {
        if (await db.Accounts.AnyAsync()) return;

        // ============================== المستوى 1 - المجموعات الرئيسية ==============================
        var assets   = Account.Create("1", "الأصول",          AccountType.Asset,     AccountNature.Debit,  null, 1, false);
        var liab     = Account.Create("2", "الخصوم",          AccountType.Liability, AccountNature.Credit, null, 1, false);
        var equity   = Account.Create("3", "حقوق الملكية",    AccountType.Equity,    AccountNature.Credit, null, 1, false);
        var revenue  = Account.Create("4", "الإيرادات",       AccountType.Revenue,   AccountNature.Credit, null, 1, false);
        var expenses = Account.Create("5", "المصروفات",       AccountType.Expense,   AccountNature.Debit,  null, 1, false);
        await db.Accounts.AddRangeAsync(assets, liab, equity, revenue, expenses);
        await db.SaveChangesAsync();

        // ============================== المستوى 2 - المجموعات الفرعية ==============================
        // 1 - الأصول
        var cash         = Account.Create("1.1", "النقدية والمصارف",            AccountType.Asset, AccountNature.Debit, assets.Id, 2, false);
        var receivables  = Account.Create("1.2", "الذمم المدينة",                AccountType.Asset, AccountNature.Debit, assets.Id, 2, false);
        var inventory    = Account.Create("1.3", "المخزون",                      AccountType.Asset, AccountNature.Debit, assets.Id, 2, false);
        var stInvest     = Account.Create("1.4", "الاستثمارات قصيرة الأجل",     AccountType.Asset, AccountNature.Debit, assets.Id, 2, false);
        var fixedAssets  = Account.Create("1.5", "الأصول الثابتة",               AccountType.Asset, AccountNature.Debit, assets.Id, 2, false);

        // 2 - الخصوم
        var clLiab       = Account.Create("2.1", "الخصوم المتداولة",            AccountType.Liability, AccountNature.Credit, liab.Id, 2, false);
        var ltLiab       = Account.Create("2.2", "الخصوم طويلة الأجل",          AccountType.Liability, AccountNature.Credit, liab.Id, 2, false);

        // 3 - حقوق الملكية
        var capital      = Account.Create("3.1", "رأس المال",                    AccountType.Equity, AccountNature.Credit, equity.Id, 2, false);
        var reserves     = Account.Create("3.2", "الاحتياطيات",                 AccountType.Equity, AccountNature.Credit, equity.Id, 2, false);
        var retained     = Account.Create("3.3", "الأرباح المحتجزة",            AccountType.Equity, AccountNature.Credit, equity.Id, 2, false);
        var partners     = Account.Create("3.4", "جاري الشركاء",                 AccountType.Equity, AccountNature.Credit, equity.Id, 2, false);

        // 4 - الإيرادات
        var opRev        = Account.Create("4.1", "الإيرادات التشغيلية",         AccountType.Revenue, AccountNature.Credit, revenue.Id, 2, false);
        var invRev       = Account.Create("4.2", "إيرادات الاستثمار",            AccountType.Revenue, AccountNature.Credit, revenue.Id, 2, false);
        var otherRev     = Account.Create("4.3", "الإيرادات الأخرى",            AccountType.Revenue, AccountNature.Credit, revenue.Id, 2, false);

        // 5 - المصروفات
        var cogs         = Account.Create("5.1", "تكلفة المبيعات",              AccountType.Expense, AccountNature.Debit, expenses.Id, 2, false);
        var sellingExp   = Account.Create("5.2", "المصروفات التسويقية والبيعية", AccountType.Expense, AccountNature.Debit, expenses.Id, 2, false);
        var adminExp     = Account.Create("5.3", "المصروفات الإدارية",           AccountType.Expense, AccountNature.Debit, expenses.Id, 2, false);
        var finExp       = Account.Create("5.4", "المصروفات التمويلية",          AccountType.Expense, AccountNature.Debit, expenses.Id, 2, false);
        var depreExp     = Account.Create("5.5", "الاندثارات (الإهلاكات)",      AccountType.Expense, AccountNature.Debit, expenses.Id, 2, false);
        var taxExp       = Account.Create("5.6", "الضرائب والرسوم",             AccountType.Expense, AccountNature.Debit, expenses.Id, 2, false);
        var otherExp     = Account.Create("5.7", "المصروفات الأخرى",            AccountType.Expense, AccountNature.Debit, expenses.Id, 2, false);

        await db.Accounts.AddRangeAsync(
            cash, receivables, inventory, stInvest, fixedAssets,
            clLiab, ltLiab,
            capital, reserves, retained, partners,
            opRev, invRev, otherRev,
            cogs, sellingExp, adminExp, finExp, depreExp, taxExp, otherExp);
        await db.SaveChangesAsync();

        // ============================== المستوى 3 - الحسابات التفصيلية (Leaf) ==============================
        var leaves = new[]
        {
            // ---------- 1.1 النقدية والمصارف ----------
            Account.Create("1.1.01", "الصندوق",                            AccountType.Asset, AccountNature.Debit, cash.Id, 3, true),
            Account.Create("1.1.02", "المصرف",                             AccountType.Asset, AccountNature.Debit, cash.Id, 3, true),
            Account.Create("1.1.03", "الشيكات تحت التحصيل",                AccountType.Asset, AccountNature.Debit, cash.Id, 3, true),

            // ---------- 1.2 الذمم المدينة ----------
            Account.Create("1.2.01", "ذمم العملاء (الذمم التجارية)",      AccountType.Asset, AccountNature.Debit, receivables.Id, 3, true),
            Account.Create("1.2.02", "ذمم الموظفين والسلف",                 AccountType.Asset, AccountNature.Debit, receivables.Id, 3, true),
            Account.Create("1.2.03", "إيرادات مستحقة القبض",                AccountType.Asset, AccountNature.Debit, receivables.Id, 3, true),
            Account.Create("1.2.04", "المصروفات المدفوعة مقدماً",          AccountType.Asset, AccountNature.Debit, receivables.Id, 3, true),
            Account.Create("1.2.05", "ذمم مدينة أخرى",                     AccountType.Asset, AccountNature.Debit, receivables.Id, 3, true),

            // ---------- 1.3 المخزون ----------
            Account.Create("1.3.01", "مخزون السلع التجارية",                AccountType.Asset, AccountNature.Debit, inventory.Id, 3, true),
            Account.Create("1.3.02", "مخزون المواد والمستلزمات",            AccountType.Asset, AccountNature.Debit, inventory.Id, 3, true),
            Account.Create("1.3.03", "بضاعة برسم الأمانة",                  AccountType.Asset, AccountNature.Debit, inventory.Id, 3, true),
            Account.Create("1.3.04", "بضاعة بالطريق",                       AccountType.Asset, AccountNature.Debit, inventory.Id, 3, true),

            // ---------- 1.4 الاستثمارات قصيرة الأجل ----------
            Account.Create("1.4.01", "ودائع لأجل",                          AccountType.Asset, AccountNature.Debit, stInvest.Id, 3, true),
            Account.Create("1.4.02", "أوراق مالية للمتاجرة",                AccountType.Asset, AccountNature.Debit, stInvest.Id, 3, true),

            // ---------- 1.5 الأصول الثابتة ----------
            Account.Create("1.5.01", "الأراضي",                             AccountType.Asset, AccountNature.Debit, fixedAssets.Id, 3, true),
            Account.Create("1.5.02", "المباني والإنشاءات",                   AccountType.Asset, AccountNature.Debit, fixedAssets.Id, 3, true),
            Account.Create("1.5.03", "الآلات والمعدات",                     AccountType.Asset, AccountNature.Debit, fixedAssets.Id, 3, true),
            Account.Create("1.5.04", "وسائل النقل",                          AccountType.Asset, AccountNature.Debit, fixedAssets.Id, 3, true),
            Account.Create("1.5.05", "الأثاث والأجهزة المكتبية",            AccountType.Asset, AccountNature.Debit, fixedAssets.Id, 3, true),
            Account.Create("1.5.06", "العدد والقوالب",                      AccountType.Asset, AccountNature.Debit, fixedAssets.Id, 3, true),
            Account.Create("1.5.07", "أجهزة الحاسوب وملحقاتها",             AccountType.Asset, AccountNature.Debit, fixedAssets.Id, 3, true),
            Account.Create("1.5.99", "مجمع الاندثار (دائن)",                AccountType.Asset, AccountNature.Credit, fixedAssets.Id, 3, true),

            // ---------- 2.1 الخصوم المتداولة ----------
            Account.Create("2.1.01", "ذمم الموردين (الذمم التجارية الدائنة)", AccountType.Liability, AccountNature.Credit, clLiab.Id, 3, true),
            Account.Create("2.1.02", "ضريبة المبيعات المستحقة الدفع",       AccountType.Liability, AccountNature.Credit, clLiab.Id, 3, true),
            Account.Create("2.1.03", "عمولات مستحقة الدفع",                  AccountType.Liability, AccountNature.Credit, clLiab.Id, 3, true),
            Account.Create("2.1.04", "رواتب وأجور مستحقة الدفع",            AccountType.Liability, AccountNature.Credit, clLiab.Id, 3, true),
            Account.Create("2.1.05", "إيرادات مقبوضة مقدماً",               AccountType.Liability, AccountNature.Credit, clLiab.Id, 3, true),
            Account.Create("2.1.06", "سلف وقروض من المصارف (قصيرة الأجل)",  AccountType.Liability, AccountNature.Credit, clLiab.Id, 3, true),
            Account.Create("2.1.07", "ذمم دائنة أخرى",                      AccountType.Liability, AccountNature.Credit, clLiab.Id, 3, true),

            // ---------- 2.2 الخصوم طويلة الأجل ----------
            Account.Create("2.2.01", "قروض طويلة الأجل",                    AccountType.Liability, AccountNature.Credit, ltLiab.Id, 3, true),
            Account.Create("2.2.02", "سندات وكمبيالات طويلة الأجل",         AccountType.Liability, AccountNature.Credit, ltLiab.Id, 3, true),
            Account.Create("2.2.03", "مخصص تعويض نهاية الخدمة",             AccountType.Liability, AccountNature.Credit, ltLiab.Id, 3, true),

            // ---------- 3.1 رأس المال ----------
            Account.Create("3.1.01", "رأس المال المدفوع",                   AccountType.Equity, AccountNature.Credit, capital.Id, 3, true),

            // ---------- 3.2 الاحتياطيات ----------
            Account.Create("3.2.01", "الاحتياطي القانوني",                   AccountType.Equity, AccountNature.Credit, reserves.Id, 3, true),
            Account.Create("3.2.02", "الاحتياطي الخاص",                     AccountType.Equity, AccountNature.Credit, reserves.Id, 3, true),
            Account.Create("3.2.03", "الاحتياطي العام",                     AccountType.Equity, AccountNature.Credit, reserves.Id, 3, true),

            // ---------- 3.3 الأرباح المحتجزة ----------
            Account.Create("3.3.01", "الأرباح المرحّلة من سنوات سابقة",     AccountType.Equity, AccountNature.Credit, retained.Id, 3, true),
            Account.Create("3.3.02", "أرباح / خسائر السنة الحالية",          AccountType.Equity, AccountNature.Credit, retained.Id, 3, true),

            // ---------- 3.4 جاري الشركاء ----------
            Account.Create("3.4.01", "جاري الشركاء",                         AccountType.Equity, AccountNature.Credit, partners.Id, 3, true),

            // ---------- 4.1 الإيرادات التشغيلية ----------
            Account.Create("4.1.01", "إيرادات المبيعات",                    AccountType.Revenue, AccountNature.Credit, opRev.Id, 3, true),
            Account.Create("4.1.02", "الخصم المسموح به (مدين)",             AccountType.Revenue, AccountNature.Debit,  opRev.Id, 3, true),
            Account.Create("4.1.03", "مردودات المبيعات (مدين)",             AccountType.Revenue, AccountNature.Debit,  opRev.Id, 3, true),
            Account.Create("4.1.04", "إيرادات الخدمات",                     AccountType.Revenue, AccountNature.Credit, opRev.Id, 3, true),

            // ---------- 4.2 إيرادات الاستثمار ----------
            Account.Create("4.2.01", "إيرادات فوائد",                       AccountType.Revenue, AccountNature.Credit, invRev.Id, 3, true),
            Account.Create("4.2.02", "إيرادات أرباح موزعة",                 AccountType.Revenue, AccountNature.Credit, invRev.Id, 3, true),

            // ---------- 4.3 الإيرادات الأخرى ----------
            Account.Create("4.3.01", "إيرادات بيع أصول ثابتة",              AccountType.Revenue, AccountNature.Credit, otherRev.Id, 3, true),
            Account.Create("4.3.02", "إيرادات متنوعة",                      AccountType.Revenue, AccountNature.Credit, otherRev.Id, 3, true),
            Account.Create("4.3.03", "أرباح فروق العملة",                   AccountType.Revenue, AccountNature.Credit, otherRev.Id, 3, true),

            // ---------- 5.1 تكلفة المبيعات ----------
            Account.Create("5.1.01", "تكلفة البضاعة المباعة",                AccountType.Expense, AccountNature.Debit,  cogs.Id, 3, true),
            Account.Create("5.1.02", "الخصم المكتسب (دائن)",                AccountType.Expense, AccountNature.Credit, cogs.Id, 3, true),
            Account.Create("5.1.03", "مردودات المشتريات (دائن)",            AccountType.Expense, AccountNature.Credit, cogs.Id, 3, true),

            // ---------- 5.2 المصروفات التسويقية والبيعية ----------
            Account.Create("5.2.01", "عمولات المندوبين",                     AccountType.Expense, AccountNature.Debit, sellingExp.Id, 3, true),
            Account.Create("5.2.02", "الإعلان والتسويق",                    AccountType.Expense, AccountNature.Debit, sellingExp.Id, 3, true),
            Account.Create("5.2.03", "النقل والشحن",                        AccountType.Expense, AccountNature.Debit, sellingExp.Id, 3, true),
            Account.Create("5.2.04", "الترويج والعينات المجانية",            AccountType.Expense, AccountNature.Debit, sellingExp.Id, 3, true),

            // ---------- 5.3 المصروفات الإدارية ----------
            Account.Create("5.3.01", "الرواتب والأجور",                     AccountType.Expense, AccountNature.Debit, adminExp.Id, 3, true),
            Account.Create("5.3.02", "الإيجارات",                           AccountType.Expense, AccountNature.Debit, adminExp.Id, 3, true),
            Account.Create("5.3.03", "الكهرباء والماء",                     AccountType.Expense, AccountNature.Debit, adminExp.Id, 3, true),
            Account.Create("5.3.04", "الاتصالات والإنترنت",                 AccountType.Expense, AccountNature.Debit, adminExp.Id, 3, true),
            Account.Create("5.3.05", "الصيانة والتصليح",                    AccountType.Expense, AccountNature.Debit, adminExp.Id, 3, true),
            Account.Create("5.3.06", "القرطاسية والمطبوعات",                 AccountType.Expense, AccountNature.Debit, adminExp.Id, 3, true),
            Account.Create("5.3.07", "الضيافة والترفيه",                    AccountType.Expense, AccountNature.Debit, adminExp.Id, 3, true),
            Account.Create("5.3.08", "أتعاب مهنية واستشارات",                AccountType.Expense, AccountNature.Debit, adminExp.Id, 3, true),

            // ---------- 5.4 المصروفات التمويلية ----------
            Account.Create("5.4.01", "الفوائد والعمولات المصرفية",          AccountType.Expense, AccountNature.Debit, finExp.Id, 3, true),
            Account.Create("5.4.02", "خسائر فروق أسعار الصرف",              AccountType.Expense, AccountNature.Debit, finExp.Id, 3, true),

            // ---------- 5.5 الاندثارات (الإهلاكات) ----------
            Account.Create("5.5.01", "اندثار المباني",                       AccountType.Expense, AccountNature.Debit, depreExp.Id, 3, true),
            Account.Create("5.5.02", "اندثار الآلات والمعدات",              AccountType.Expense, AccountNature.Debit, depreExp.Id, 3, true),
            Account.Create("5.5.03", "اندثار وسائل النقل",                  AccountType.Expense, AccountNature.Debit, depreExp.Id, 3, true),
            Account.Create("5.5.04", "اندثار الأثاث والأجهزة",              AccountType.Expense, AccountNature.Debit, depreExp.Id, 3, true),

            // ---------- 5.6 الضرائب والرسوم ----------
            Account.Create("5.6.01", "ضريبة الدخل",                          AccountType.Expense, AccountNature.Debit, taxExp.Id, 3, true),
            Account.Create("5.6.02", "الرسوم الحكومية",                     AccountType.Expense, AccountNature.Debit, taxExp.Id, 3, true),
            Account.Create("5.6.03", "الاشتراكات والاتحادات",                AccountType.Expense, AccountNature.Debit, taxExp.Id, 3, true),

            // ---------- 5.7 المصروفات الأخرى ----------
            Account.Create("5.7.01", "خسائر بيع أصول ثابتة",                AccountType.Expense, AccountNature.Debit, otherExp.Id, 3, true),
            Account.Create("5.7.02", "مصروفات متنوعة",                      AccountType.Expense, AccountNature.Debit, otherExp.Id, 3, true),
        };

        await db.Accounts.AddRangeAsync(leaves);
        await db.SaveChangesAsync();
    }
}
