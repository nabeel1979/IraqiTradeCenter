using IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Seed;

/// <summary>
/// تعبئة NameEn لكل أسماء الدليل المحاسبي العراقي الموحّد (UCAS) +
/// أنواع السندات + الصناديق التي أُدخلت بدون اسم إنجليزي، حتى تعمل
/// الواجهة الإنجليزية دون نصوص عربية متناثرة.
///
/// الـ join يتم على <c>Code</c> (مفتاح مستقر يطابق نظام UCAS الرسمي) ويُجبر
/// إعادة الكتابة لتصحيح القيم العَرَضية المعيبة. آمن للتشغيل المتكرر.
/// </summary>
public static class NameEnBackfill
{
    public static async Task RunAsync(AccountingDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(AccountsSql, ct);
        await db.Database.ExecuteSqlRawAsync(VoucherTypesSql, ct);
        await db.Database.ExecuteSqlRawAsync(CashBoxesSql, ct);
    }

    private const string AccountsSql = @"
;WITH m(Code, NameEn) AS (
    SELECT * FROM (VALUES
        -- ═══ مستوى 1 — الجذور الأربعة (UCAS) ═══
        ('1',   N'Assets'),
        ('2',   N'Liabilities'),
        ('3',   N'Expenses'),
        ('4',   N'Revenue'),

        -- ═══ مستوى 2 — المجموعات الفرعية ═══
        ('11',  N'Fixed Assets'),
        ('12',  N'Projects Under Construction'),
        ('13',  N'Inventory'),
        ('14',  N'Loans Granted'),
        ('15',  N'Financial Investments'),
        ('16',  N'Receivables'),
        ('18',  N'Cash'),
        ('19',  N'Debit Contra Accounts'),

        ('21',  N'Capital'),
        ('22',  N'Reserves'),
        ('23',  N'Provisions'),
        ('24',  N'Loans Received'),
        ('25',  N'Bank Overdrafts'),
        ('26',  N'Payables'),
        ('28',  N'Current Operations Account'),
        ('29',  N'Credit Contra Accounts'),

        ('31',  N'Salaries & Wages'),
        ('32',  N'Material Requirements'),
        ('33',  N'Service Requirements'),
        ('34',  N'Contracts & Services'),
        ('35',  N'Purchases for Resale'),
        ('36',  N'Interest & Land Rents'),
        ('37',  N'Depreciation'),
        ('38',  N'Transfer Expenses'),
        ('39',  N'Other Expenses'),

        ('41',  N'Goods Production Revenue'),
        ('42',  N'Commercial Activity Revenue'),
        ('43',  N'Service Activity Revenue'),
        ('44',  N'Outsourced Operation Revenue'),
        ('45',  N'Cost of Internally Manufactured Assets'),
        ('46',  N'Interest & Land Rents (Revenue)'),
        ('47',  N'Subsidies'),
        ('48',  N'Transfer Revenue'),
        ('49',  N'Other Revenue'),

        -- ═══ مستوى 3 — 11 الموجودات الثابتة ═══
        ('111', N'Land'),
        ('112', N'Buildings, Constructions & Roads'),
        ('113', N'Machinery & Equipment'),
        ('114', N'Vehicles & Transport'),
        ('115', N'Tools & Dies'),
        ('116', N'Furniture & Office Equipment'),
        ('117', N'Plants & Animals'),
        ('118', N'Deferred Revenue Expenditures'),

        -- ═══ مستوى 3 — 12 مشاريع تحت التنفيذ ═══
        ('121', N'Land (Under Construction)'),
        ('122', N'Buildings & Constructions (Under Construction)'),
        ('123', N'Machinery & Equipment (Under Construction)'),
        ('124', N'Vehicles & Transport (Under Construction)'),
        ('125', N'Tools & Dies (Under Construction)'),
        ('126', N'Furniture & Office Equipment (Under Construction)'),
        ('127', N'Plants & Animals (Under Construction)'),
        ('128', N'Deferred Revenue Expenditures (Under Construction)'),
        ('129', N'Investment Spending'),

        -- ═══ مستوى 3 — 13 المخزون ═══
        ('131', N'Raw Materials Warehouse'),
        ('132', N'Fuel & Oil Warehouse'),
        ('133', N'Spare Parts Warehouse'),
        ('134', N'Packaging Materials Warehouse'),
        ('135', N'Miscellaneous Warehouse'),
        ('136', N'Production Warehouse'),
        ('137', N'Goods-for-Sale Warehouse'),
        ('138', N'Material Purchase L/Cs'),
        ('139', N'Other Materials Warehouse'),

        -- ═══ مستوى 3 — 14 القروض الممنوحة ═══
        ('141', N'Long-term Loans Granted'),
        ('142', N'Short-term Loans Granted'),

        -- ═══ مستوى 3 — 15 الاستثمارات المالية ═══
        ('151', N'Long-term Investments'),
        ('152', N'Short-term Investments'),

        -- ═══ مستوى 3 — 16 المدينون ═══
        ('161', N'Customers'),
        ('162', N'Notes Receivable'),
        ('163', N'Debit Current Accounts'),
        ('164', N'Secondary Commitments Advances'),
        ('165', N'Non-Current Activity Receivables'),
        ('166', N'Miscellaneous Receivables'),
        ('167', N'Advances'),

        -- ═══ مستوى 3 — 18 النقود ═══
        ('181', N'Cash on Hand'),
        ('182', N'Permanent Advances'),
        ('183', N'Cash at Banks'),
        ('184', N'Cash in Vaults'),
        ('185', N'Checks & Remittances'),

        -- ═══ مستوى 3 — 19 الحسابات المتقابلة المدينة ═══
        ('191', N'Finished Production Movement at Selling Price'),
        ('192', N'Debit Commitment Accounts'),
        ('193', N'Nominal Value Accounts'),
        ('194', N'Debit Result Accounts'),

        -- ═══ مستوى 3 — 21 رأس المال ═══
        ('211', N'Paid-in Capital'),
        ('212', N'Capital (Net Assets)'),

        -- ═══ مستوى 3 — 22 الاحتياطيات ═══
        ('221', N'Capital Reserves'),
        ('222', N'General Reserve'),
        ('223', N'Miscellaneous Reserves'),
        ('224', N'Accumulated Surplus'),
        ('225', N'Accumulated Deficit (Debit)'),

        -- ═══ مستوى 3 — 23 التخصيصات ═══
        ('231', N'Accumulated Depreciation Provision'),
        ('232', N'Doubtful Debts Provision'),
        ('234', N'Purchase Expenses Provision'),
        ('235', N'Miscellaneous Provisions'),
        ('238', N'Financial Investments Decline Provision'),

        -- ═══ مستوى 3 — 24 القروض المستلمة ═══
        ('241', N'Long-term Loans Received'),
        ('242', N'Short-term Loans Received'),

        -- ═══ مستوى 3 — 25 المصارف الدائنة ═══
        ('251', N'Bank Overdraft'),

        -- ═══ مستوى 3 — 26 الدائنون ═══
        ('261', N'Suppliers'),
        ('262', N'Notes Payable'),
        ('263', N'Credit Current Accounts'),
        ('264', N'Commitment Accounts'),
        ('265', N'Non-Current Activity Payables'),
        ('266', N'Miscellaneous Payables'),
        ('267', N'Deductions for Third Parties'),
        ('268', N'Dividend Distribution Payables'),

        -- ═══ مستوى 3 — 28 حساب العمليات الجارية ═══
        ('281', N'Current Activity Account'),

        -- ═══ مستوى 3 — 29 الحسابات المتقابلة الدائنة ═══
        ('291', N'Counter Finished Production Movement at Selling Price'),
        ('292', N'Credit Commitment Accounts'),
        ('293', N'Counter Nominal Value Accounts'),
        ('294', N'Credit Result Accounts'),

        -- ═══ مستوى 3 — 31 رواتب واجور ═══
        ('311', N'Employee Cash Salaries'),
        ('312', N'Worker Cash Wages'),
        ('313', N'Non-Iraqi Salaries, Wages & Allowances'),
        ('314', N'Employee Social Security Contributions'),
        ('315', N'Worker Social Security Contributions'),
        ('316', N'Non-Iraqi Social Security Contributions'),

        -- ═══ مستوى 3 — 32 المستلزمات السلعية ═══
        ('321', N'Services & Raw Materials'),
        ('322', N'Fuel & Oils'),
        ('323', N'Spare Parts'),
        ('324', N'Packaging Materials'),
        ('325', N'Miscellaneous Supplies'),
        ('326', N'Worker Equipment'),
        ('327', N'Water & Electricity'),

        -- ═══ مستوى 3 — 33 المستلزمات الخدمية ═══
        ('331', N'Maintenance Services'),
        ('332', N'Research & Consulting Services'),
        ('333', N'Advertising, Printing & Hospitality'),
        ('334', N'Transport, Delegation & Telecom'),
        ('335', N'Fixed Asset Rentals'),
        ('336', N'Miscellaneous Service Expenses'),

        -- ═══ مستوى 3 — 34 مقاولات وخدمات ═══
        ('341', N'Legal Contracts'),
        ('342', N'Operating Services'),

        -- ═══ مستوى 3 — 35 مشتريات البضائع بغرض البيع ═══
        ('351', N'Local Purchases for Resale'),
        ('352', N'Imported Purchases for Resale'),

        -- ═══ مستوى 3 — 36 الفوائد وايجارات الاراضي ═══
        ('361', N'Interest Expense'),
        ('362', N'Land Rents'),

        -- ═══ مستوى 3 — 37 الاندثار ═══
        ('372', N'Depreciation of Buildings, Constructions & Roads'),
        ('373', N'Depreciation of Machinery & Equipment'),
        ('374', N'Depreciation of Vehicles & Transport'),
        ('375', N'Depreciation of Tools & Dies'),
        ('376', N'Depreciation of Furniture & Office Equipment'),
        ('377', N'Depreciation of Plants & Animals'),
        ('378', N'Amortization of Deferred Expenses'),

        -- ═══ مستوى 3 — 38 المصروفات التحويلية ═══
        ('381', N'Retirement & Social Security Expenses'),
        ('382', N'Contributions to Central or Subsidiary Units'),
        ('383', N'Miscellaneous Transfer Expenses'),
        ('384', N'Taxes & Fees'),
        ('385', N'Subsidies'),

        -- ═══ مستوى 3 — 39 المصروفات الاخرى ═══
        ('391', N'Prior Year Expenses'),
        ('392', N'Incidental Expenses'),
        ('393', N'Capital Losses'),
        ('397', N'Stock Decline Reserve'),

        -- ═══ مستوى 3 — 41 ايراد نشاط الانتاج السلعي ═══
        ('411', N'Extractive Industries Activity Revenue'),
        ('412', N'Manufacturing Activity Revenue'),
        ('413', N'Construction Activity Revenue'),
        ('414', N'Plant Production Activity Revenue'),
        ('415', N'Animal Production Activity Revenue'),
        ('416', N'Water & Electricity Revenue'),
        ('417', N'By-product Sales Revenue'),

        -- ═══ مستوى 3 — 42 ايراد النشاط التجاري ═══
        ('421', N'Net Sales of Goods for Resale'),
        ('422', N'Change in Goods-for-Resale Inventory'),
        ('423', N'Commission Received'),
        ('424', N'Hotel & Tourism Revenue'),
        ('425', N'Miscellaneous Revenue'),

        -- ═══ مستوى 3 — 43 ايراد النشاط الخدمي ═══
        ('431', N'Transport Services Revenue'),
        ('432', N'Telecom Services Revenue'),
        ('433', N'Maintenance & Repair Services Revenue'),
        ('434', N'Consulting & Technical Services Revenue'),
        ('435', N'Social Services Revenue'),
        ('436', N'Membership & Subscription Revenue'),
        ('437', N'Miscellaneous Services Revenue'),
        ('438', N'Fixed Asset Rentals (Excl. Land)'),

        -- ═══ مستوى 3 — 44 ايراد التشغيل للغير ═══
        ('441', N'Outsourced Operation Revenue'),

        -- ═══ مستوى 3 — 45 كلفة الموجودات المصنعة داخليا ═══
        ('451', N'Cost of Manufactured Fixed Assets'),
        ('452', N'Cost of Manufactured Spare Parts'),
        ('453', N'Cost of Manufactured Packaging Materials'),

        -- ═══ مستوى 3 — 46 الفوائد وايجارات الاراضي (إيراد) ═══
        ('461', N'Interest Income'),
        ('462', N'Land Rents Income'),
        ('463', N'Financial Investments Income'),

        -- ═══ مستوى 3 — 47 الاعانات ═══
        ('471', N'Imported Goods Subsidies'),
        ('472', N'Local Production Subsidies'),
        ('473', N'Export Subsidies'),
        ('474', N'Other Subsidies'),

        -- ═══ مستوى 3 — 48 الايرادات التحويلية ═══
        ('481', N'Retirement & Social Security Revenue'),
        ('482', N'Funding Grants'),
        ('483', N'Miscellaneous Transfer Revenue'),

        -- ═══ مستوى 3 — 49 الايرادات الاخرى ═══
        ('491', N'Prior Year Revenue'),
        ('492', N'Incidental Revenue'),
        ('493', N'Capital Revenue')
    ) AS v(Code, NameEn)
)
UPDATE a
SET    a.NameEn    = m.NameEn,
       a.UpdatedAt = SYSUTCDATETIME(),
       a.UpdatedBy = ISNULL(a.UpdatedBy, N'system')
FROM   [acc].[Accounts] a
JOIN   m ON LTRIM(RTRIM(a.Code)) = m.Code;
";

    /// <summary>
    /// أنواع السندات — مفتاح Code.
    /// </summary>
    private const string VoucherTypesSql = @"
;WITH m(Code, NameEn) AS (
    SELECT * FROM (VALUES
        ('JV', N'Journal Voucher'),
        ('RV', N'Receipt Voucher'),
        ('PV', N'Payment Voucher'),
        ('AV', N'Adjustment Voucher'),
        ('OV', N'Opening Voucher'),
        ('CV', N'Closing Voucher')
    ) AS v(Code, NameEn)
)
UPDATE t
SET    t.NameEn    = m.NameEn,
       t.UpdatedAt = SYSUTCDATETIME(),
       t.UpdatedBy = ISNULL(t.UpdatedBy, N'system')
FROM   [acc].[JournalVoucherTypes] t
JOIN   m ON LTRIM(RTRIM(t.Code)) = m.Code
WHERE  (t.NameEn IS NULL OR LTRIM(RTRIM(t.NameEn)) = N'');
";

    /// <summary>
    /// الصناديق المُسماة عربياً بنمط شائع — نُغطّيها كاحتياط فقط.
    /// </summary>
    private const string CashBoxesSql = @"
;WITH m(NameAr, NameEn) AS (
    SELECT * FROM (VALUES
        (N'الصندوق الرئيسي',  N'Main Cash Box'),
        (N'الصندوق الفرعي',   N'Secondary Cash Box'),
        (N'صندوق المبيعات',   N'Sales Cash Box'),
        (N'صندوق المشتريات',  N'Purchases Cash Box'),
        (N'صندوق المصاريف',   N'Petty Cash'),
        (N'صندوق الفرع',      N'Branch Cash Box'),
        (N'صندوق دينار',      N'IQD Cash Box'),
        (N'صندوق دولار',      N'USD Cash Box'),
        (N'الصندوق',          N'Cash Box')
    ) AS v(NameAr, NameEn)
)
UPDATE c
SET    c.NameEn    = m.NameEn,
       c.UpdatedAt = SYSUTCDATETIME(),
       c.UpdatedBy = ISNULL(c.UpdatedBy, N'system')
FROM   [acc].[CashBoxes] c
JOIN   m ON LTRIM(RTRIM(c.NameAr)) = m.NameAr
WHERE  (c.NameEn IS NULL OR LTRIM(RTRIM(c.NameEn)) = N'');
";
}
