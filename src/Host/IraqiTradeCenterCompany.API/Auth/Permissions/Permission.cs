namespace IraqiTradeCenterCompany.API.Auth.Permissions;

/// <summary>
/// كل صلاحية مفردة في النظام. الجدول مُولَّد تلقائياً من <see cref="PermissionRegistry"/>
/// عند بدء التطبيق، فلا يُحرَّر يدوياً — مجرد كتالوج للقراءة من الواجهة.
/// </summary>
public class Permission
{
    /// <summary>المفتاح الفريد، صيغة: <c>Module.Resource.Action</c> — مثلاً <c>Accounting.JournalEntries.Post</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>القسم الرئيسي (المحاسبة / المبيعات / المخزون / النظام...).</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>المورد داخل القسم (JournalEntries / Customers / CashBoxes...).</summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>نوع العملية (Read / Create / Update / Delete / Print / Post / Export).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>اسم عربي لطيف للعرض في شجرة الصلاحيات.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>وصف مساعد يظهر كـ tooltip في الواجهة.</summary>
    public string? Description { get; set; }

    /// <summary>ترتيب العرض داخل المجموعة (1, 2, 3...).</summary>
    public int DisplayOrder { get; set; }
}
