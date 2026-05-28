namespace IraqiTradeCenterCompany.API.Auth.Permissions;

/// <summary>
/// المصدر الوحيد لكل أكواد الصلاحيات في النظام.
///
/// لإضافة مورد جديد:
///   1) أضف static class داخل القسم المناسب (مثلاً <see cref="Accounting"/>).
///   2) عرّف ثوابت الأكواد كـ <c>public const string</c>.
///   3) أضف عنصراً في <see cref="GetAll"/> يصف المورد وعملياته.
///
/// عند بدء التطبيق، يقرأ <c>PermissionsBootstrapper</c> هذا السجل ويُسقطه في الجدول
/// (insert ما هو جديد، حذف ما لم يعد موجوداً) بحيث يبقى الجدول مطابقاً للكود تماماً.
/// </summary>
public static class PermissionRegistry
{
    // ────────────────────────────────────────────────────────────
    //  ثوابت الأقسام والعمليات لتوحيد التسميات وتجنّب الأخطاء الإملائية
    // ────────────────────────────────────────────────────────────
    public static class Modules
    {
        public const string Accounting = "Accounting";
        public const string Sales      = "Sales";
        public const string Inventory  = "Inventory";
        public const string System     = "System";
    }

    public static class Actions
    {
        public const string Read    = "Read";
        public const string Create  = "Create";
        public const string Update  = "Update";
        public const string Delete  = "Delete";
        public const string Print   = "Print";
        public const string Export  = "Export";
        public const string Post    = "Post"; // خاص بالقيود/السندات
        public const string Restore = "Restore"; // خاص بسلة المهملات
        public const string Purge   = "Purge";   // حذف نهائي من السلة
        public const string Receive = "Receive"; // تأكيد/تراجع استلام (مناقلات الصناديق)
        public const string Cancel  = "Cancel";  // إلغاء عملية بحالة معلَّقة (مثل المناقلات)
        public const string Apply    = "Apply";    // تطبيق شفرة ترخيص
        public const string Generate = "Generate"; // توليد شفرة ترخيص
        public const string Topup    = "Topup";    // شحن المحفظة
        public const string ViewAll  = "ViewAll";  // تجاوز فلترة الصناديق المسموحة (يرى كل الصناديق/السندات)
    }

    public static readonly Dictionary<string, string> ActionLabelsAr = new()
    {
        [Actions.Read]    = "قراءة",
        [Actions.Create]  = "إضافة",
        [Actions.Update]  = "تعديل",
        [Actions.Delete]  = "حذف",
        [Actions.Print]   = "طباعة",
        [Actions.Export]  = "تصدير",
        [Actions.Post]    = "ترحيل",
        [Actions.Restore] = "استعادة من",
        [Actions.Purge]   = "حذف نهائي من",
        [Actions.Receive]  = "استلام",
        [Actions.Cancel]   = "إلغاء",
        [Actions.Apply]    = "تطبيق",
        [Actions.Generate] = "توليد",
        [Actions.Topup]    = "شحن",
        [Actions.ViewAll]  = "عرض جميع",
    };

    public static readonly Dictionary<string, string> ModuleLabelsAr = new()
    {
        [Modules.Accounting] = "المحاسبة",
        [Modules.Sales]      = "المبيعات",
        [Modules.Inventory]  = "المخزون",
        [Modules.System]     = "النظام",
    };

    // ────────────────────────────────────────────────────────────
    //  أكواد الصلاحيات — مُجمَّعة حسب المورد لسهولة الاستخدام
    //  داخل [RequirePermission(Permissions.Accounting.JournalEntries.Post)]
    // ────────────────────────────────────────────────────────────

    public static class Accounting
    {
        public static class JournalEntries
        {
            public const string Read   = "Accounting.JournalEntries.Read";
            public const string Create = "Accounting.JournalEntries.Create";
            public const string Update = "Accounting.JournalEntries.Update";
            public const string Delete = "Accounting.JournalEntries.Delete";
            public const string Print  = "Accounting.JournalEntries.Print";
            public const string Post   = "Accounting.JournalEntries.Post";
        }
        /// <summary>
        /// صلاحيات السندات تُولَّد ديناميكياً لكل نوع سند منشأ في
        /// <c>acc.JournalVoucherTypes</c> بصيغة:
        /// <c>Accounting.Vouchers.{CODE}.{Action}</c>
        /// انظر <see cref="VoucherTypePermissionFactory"/>.
        /// </summary>
        public static class Vouchers
        {
            public const string Prefix = "Accounting.Vouchers.";
            public const string Resource = "Vouchers";
            public static string Read(string code)   => $"{Prefix}{code.ToUpperInvariant()}.{Actions.Read}";
            public static string Create(string code) => $"{Prefix}{code.ToUpperInvariant()}.{Actions.Create}";
            public static string Update(string code) => $"{Prefix}{code.ToUpperInvariant()}.{Actions.Update}";
            public static string Delete(string code) => $"{Prefix}{code.ToUpperInvariant()}.{Actions.Delete}";
            public static string Print(string code)  => $"{Prefix}{code.ToUpperInvariant()}.{Actions.Print}";
            public static string Post(string code)   => $"{Prefix}{code.ToUpperInvariant()}.{Actions.Post}";
        }
        public static class Accounts
        {
            public const string Read   = "Accounting.Accounts.Read";
            public const string Create = "Accounting.Accounts.Create";
            public const string Update = "Accounting.Accounts.Update";
            public const string Delete = "Accounting.Accounts.Delete";
            public const string Print  = "Accounting.Accounts.Print";
        }
        public static class CashBoxes
        {
            public const string Read    = "Accounting.CashBoxes.Read";
            public const string Create  = "Accounting.CashBoxes.Create";
            public const string Update  = "Accounting.CashBoxes.Update";
            public const string Delete  = "Accounting.CashBoxes.Delete";
            /// <summary>
            /// تجاوز فلترة الصناديق المسموحة — يسمح للمستخدم برؤية جميع
            /// السندات/الأرصدة/المناقلات بغضّ النظر عن الصناديق المخصّصة له.
            /// (مفيدة للمدراء والمحاسبين الذين لا يرتبطون بصندوق محدّد.)
            /// </summary>
            public const string ViewAll = "Accounting.CashBoxes.ViewAll";
        }
        public static class CashBoxBalances
        {
            public const string Read  = "Accounting.CashBoxBalances.Read";
            public const string Print = "Accounting.CashBoxBalances.Print";
        }
        public static class CashBoxTransfers
        {
            public const string Read    = "Accounting.CashBoxTransfers.Read";
            public const string Create  = "Accounting.CashBoxTransfers.Create";
            public const string Update  = "Accounting.CashBoxTransfers.Update";
            public const string Delete  = "Accounting.CashBoxTransfers.Delete";
            public const string Receive = "Accounting.CashBoxTransfers.Receive";
            public const string Cancel  = "Accounting.CashBoxTransfers.Cancel";
            public const string Print   = "Accounting.CashBoxTransfers.Print";
        }
        public static class TrialBalance
        {
            public const string Read   = "Accounting.TrialBalance.Read";
            public const string Print  = "Accounting.TrialBalance.Print";
            public const string Export = "Accounting.TrialBalance.Export";
        }
        public static class AccountStatement
        {
            public const string Read   = "Accounting.AccountStatement.Read";
            public const string Print  = "Accounting.AccountStatement.Print";
            public const string Export = "Accounting.AccountStatement.Export";
        }
        public static class FiscalYears
        {
            public const string Read   = "Accounting.FiscalYears.Read";
            public const string Create = "Accounting.FiscalYears.Create";
            public const string Update = "Accounting.FiscalYears.Update";
            public const string Delete = "Accounting.FiscalYears.Delete";
        }
        public static class CurrencyRates
        {
            public const string Read   = "Accounting.CurrencyRates.Read";
            public const string Create = "Accounting.CurrencyRates.Create";
            public const string Update = "Accounting.CurrencyRates.Update";
            public const string Delete = "Accounting.CurrencyRates.Delete";
        }
        public static class VoucherTypes
        {
            public const string Read   = "Accounting.VoucherTypes.Read";
            public const string Create = "Accounting.VoucherTypes.Create";
            public const string Update = "Accounting.VoucherTypes.Update";
            public const string Delete = "Accounting.VoucherTypes.Delete";
        }
    }

    public static class Sales
    {
        public static class Invoices
        {
            public const string Read   = "Sales.Invoices.Read";
            public const string Create = "Sales.Invoices.Create";
            public const string Update = "Sales.Invoices.Update";
            public const string Delete = "Sales.Invoices.Delete";
            public const string Print  = "Sales.Invoices.Print";
        }
        public static class Customers
        {
            public const string Read   = "Sales.Customers.Read";
            public const string Create = "Sales.Customers.Create";
            public const string Update = "Sales.Customers.Update";
            public const string Delete = "Sales.Customers.Delete";
        }
        public static class SalesReps
        {
            public const string Read   = "Sales.SalesReps.Read";
            public const string Create = "Sales.SalesReps.Create";
            public const string Update = "Sales.SalesReps.Update";
            public const string Delete = "Sales.SalesReps.Delete";
        }
        public static class Orders
        {
            public const string Read   = "Sales.Orders.Read";
            public const string Update = "Sales.Orders.Update";
        }
    }

    public static class Inventory
    {
        public static class Items
        {
            public const string Read   = "Inventory.Items.Read";
            public const string Create = "Inventory.Items.Create";
            public const string Update = "Inventory.Items.Update";
            public const string Delete = "Inventory.Items.Delete";
        }
        public static class Movements
        {
            public const string Read   = "Inventory.Movements.Read";
            public const string Create = "Inventory.Movements.Create";
        }
    }

    public static class System
    {
        public static class Users
        {
            public const string Read   = "System.Users.Read";
            public const string Create = "System.Users.Create";
            public const string Update = "System.Users.Update";
            public const string Delete = "System.Users.Delete";
        }
        public static class Roles
        {
            public const string Read   = "System.Roles.Read";
            public const string Create = "System.Roles.Create";
            public const string Update = "System.Roles.Update";
            public const string Delete = "System.Roles.Delete";
        }
        public static class CompanySettings
        {
            public const string Read   = "System.CompanySettings.Read";
            public const string Update = "System.CompanySettings.Update";
        }
        public static class Trash
        {
            public const string Read    = "System.Trash.Read";
            public const string Restore = "System.Trash.Restore";
            public const string Purge   = "System.Trash.Purge";
        }
        /// <summary>
        /// ترخيص النظام: قراءة الحالة وتطبيق الشفرات. <see cref="Generate"/>
        /// مخصّصة للمسؤول/مالك مركز التجارة العراقي (تُمنح يدوياً فقط).
        /// </summary>
        public static class License
        {
            public const string Read     = "System.License.Read";
            public const string Apply    = "System.License.Apply";
            public const string Generate = "System.License.Generate";
        }
        /// <summary>محفظة الشركة المالية (لشراء التراخيص).</summary>
        public static class Wallet
        {
            public const string Read  = "System.Wallet.Read";
            public const string Topup = "System.Wallet.Topup";
        }
        /// <summary>
        /// سجل المراقبة (Audit Log) — يعرض تاريخ كل عملية حسّاسة في النظام.
        /// قراءته حسّاسة (يكشف نشاط المستخدمين الآخرين)، فلا يمنح إلا للمدراء.
        /// </summary>
        public static class Audit
        {
            public const string Read  = "System.Audit.Read";
            public const string Export = "System.Audit.Export";
        }
    }

    // ────────────────────────────────────────────────────────────
    //  هنا التوصيف الكامل: كل مورد + عملياته + ترتيب العرض في الواجهة
    //  أضف موارد جديدة هنا فقط، الباقي يحدث تلقائياً.
    // ────────────────────────────────────────────────────────────
    public static IEnumerable<Permission> GetAll()
    {
        // المحاسبة
        foreach (var p in Resource(Modules.Accounting, "JournalEntries", "القيود اليومية", order: 11,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete, Actions.Print, Actions.Post)) yield return p;

        // ‎صلاحيات السندات (قبض/دفع/تسوية/...) تُولَّد ديناميكياً لكل نوع سند
        // ‎من خلال VoucherTypePermissionFactory، فلا تُسرد هنا.

        foreach (var p in Resource(Modules.Accounting, "Accounts", "شجرة الحسابات", order: 13,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete, Actions.Print)) yield return p;

        foreach (var p in Resource(Modules.Accounting, "TrialBalance", "ميزان المراجعة", order: 14,
            Actions.Read, Actions.Print, Actions.Export)) yield return p;

        foreach (var p in Resource(Modules.Accounting, "AccountStatement", "كشف الحساب", order: 15,
            Actions.Read, Actions.Print, Actions.Export)) yield return p;

        // ‎صفحة الصناديق فيها ثلاث تبويبات (الصناديق / الأرصدة / المناقلات) — لكلٍّ
        // ‎مورد صلاحيات منفصل كي يمكن منح المحاسب الاطلاع على الأرصدة فقط، أو
        // ‎منح أمين الصندوق صلاحية الاستلام دون الحذف، …إلخ.
        foreach (var p in Resource(Modules.Accounting, "CashBoxes", "الصناديق", order: 16,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete, Actions.ViewAll)) yield return p;

        foreach (var p in Resource(Modules.Accounting, "CashBoxBalances", "أرصدة الصناديق", order: 165,
            Actions.Read, Actions.Print)) yield return p;

        foreach (var p in Resource(Modules.Accounting, "CashBoxTransfers", "مناقلات الصناديق", order: 166,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete,
            Actions.Receive, Actions.Cancel, Actions.Print)) yield return p;

        foreach (var p in Resource(Modules.Accounting, "FiscalYears", "الفترات المحاسبية", order: 17,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete)) yield return p;

        foreach (var p in Resource(Modules.Accounting, "CurrencyRates", "نشرات أسعار العملات", order: 18,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete)) yield return p;

        foreach (var p in Resource(Modules.Accounting, "VoucherTypes", "أنواع السندات", order: 19,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete)) yield return p;

        // المبيعات
        foreach (var p in Resource(Modules.Sales, "Invoices", "الفواتير", order: 21,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete, Actions.Print)) yield return p;

        foreach (var p in Resource(Modules.Sales, "Customers", "العملاء", order: 22,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete)) yield return p;

        foreach (var p in Resource(Modules.Sales, "SalesReps", "المندوبون", order: 23,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete)) yield return p;

        foreach (var p in Resource(Modules.Sales, "Orders", "الطلبيات الواردة", order: 24,
            Actions.Read, Actions.Update)) yield return p;

        // المخزون
        foreach (var p in Resource(Modules.Inventory, "Items", "المواد", order: 31,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete)) yield return p;

        foreach (var p in Resource(Modules.Inventory, "Movements", "حركات المخزون", order: 32,
            Actions.Read, Actions.Create)) yield return p;

        // النظام
        foreach (var p in Resource(Modules.System, "Users", "المستخدمين", order: 41,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete)) yield return p;

        foreach (var p in Resource(Modules.System, "Roles", "الأدوار والصلاحيات", order: 42,
            Actions.Read, Actions.Create, Actions.Update, Actions.Delete)) yield return p;

        foreach (var p in Resource(Modules.System, "CompanySettings", "إعدادات الشركة", order: 43,
            Actions.Read, Actions.Update)) yield return p;

        // ‎سلة المهملات الموحَّدة — صلاحيات منفصلة لأن "الحذف النهائي" أخطر من "الاستعادة"
        // ‎والإثنان أخطر من مجرد "القراءة" (تصفّح ما هو محذوف).
        foreach (var p in Resource(Modules.System, "Trash", "سلة المهملات", order: 44,
            Actions.Read, Actions.Restore, Actions.Purge)) yield return p;

        // ‎ترخيص النظام: قراءة الحالة (الكل)، تطبيق شفرة (المدير)، توليد شفرة (المسؤول الأعلى فقط).
        foreach (var p in Resource(Modules.System, "License", "ترخيص النظام", order: 45,
            Actions.Read, Actions.Apply, Actions.Generate)) yield return p;

        // ‎المحفظة المالية للشركة — تُستخدم لشراء التراخيص.
        foreach (var p in Resource(Modules.System, "Wallet", "محفظة الشركة", order: 46,
            Actions.Read, Actions.Topup)) yield return p;

        // ‎سجل المراقبة (Audit) — يعرض تاريخ كل عملية حسّاسة (إضافة/تعديل/حذف/طباعة).
        // ‎القراءة فقط (مع تصدير)؛ لا يوجد تعديل/حذف لأن السجل append-only.
        foreach (var p in Resource(Modules.System, "Audit", "سجل المراقبة", order: 47,
            Actions.Read, Actions.Export)) yield return p;
    }

    private static IEnumerable<Permission> Resource(string module, string resource, string nameAr, int order, params string[] actions)
    {
        var i = 0;
        foreach (var act in actions)
        {
            yield return new Permission
            {
                Code         = $"{module}.{resource}.{act}",
                Module       = module,
                Resource     = resource,
                Action       = act,
                NameAr       = $"{ActionLabelsAr[act]} {nameAr}",
                DisplayOrder = order * 100 + (i++),
            };
        }
    }
}
