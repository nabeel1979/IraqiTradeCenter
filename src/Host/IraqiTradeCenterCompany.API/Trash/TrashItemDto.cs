namespace IraqiTradeCenterCompany.API.Trash;

/// <summary>
/// عنصر مُسطَّح في سلة المهملات الموحَّدة. كل ITrashProvider يحوّل كيانه إلى هذا الشكل.
/// الواجهة الأمامية تستهلكه دون الحاجة لمعرفة بنية الكيان الأصلي.
/// </summary>
public class TrashItemDto
{
    /// <summary>المُعرّف التقني للنوع — يستخدم في endpoints الاستعادة والحذف النهائي.</summary>
    public string EntityType { get; set; } = default!;

    /// <summary>التسمية العربية المعروضة للنوع (مثل "حساب"، "صندوق"، "نوع سند").</summary>
    public string EntityTypeLabel { get; set; } = default!;

    /// <summary>اسم المودول لتجميع العناصر في الواجهة (مثل "المحاسبة").</summary>
    public string Module { get; set; } = default!;

    /// <summary>الاسم الذي تُعرف به أيقونة lucide-react في الواجهة (مثل "Wallet").</summary>
    public string Icon { get; set; } = "Trash2";

    /// <summary>المعرّف الفريد للسجل داخل النوع.</summary>
    public int EntityId { get; set; }

    /// <summary>الكود/الرقم إن وُجد (يُعرض بخط num-display).</summary>
    public string? Code { get; set; }

    /// <summary>الاسم/العنوان الرئيسي للعرض.</summary>
    public string DisplayName { get; set; } = default!;

    /// <summary>سياق إضافي (مثل "تحت 181 — شدّة بالصندوق"، أو "بتاريخ 2026/01/15").</summary>
    public string? SubInfo { get; set; }

    /// <summary>تاريخ الحذف الناعم (DeletedAt في BaseEntity).</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>من قام بالحذف (UpdatedBy المُسجَّل وقت MarkAsDeleted).</summary>
    public string? DeletedBy { get; set; }

    /// <summary>هل يمكن استعادة العنصر؟ (false عند وجود تبعية محذوفة أيضاً).</summary>
    public bool CanRestore { get; set; } = true;

    /// <summary>سبب عدم إمكانية الاستعادة (يظهر كـ tooltip في الواجهة).</summary>
    public string? CannotRestoreReason { get; set; }

    /// <summary>
    /// هل يسمح بالحذف النهائي؟ يُستخدم لحجب زر "حذف نهائي" في الواجهة على
    /// عناصر الخطأ الوهمية (التي يُنتجها <c>TrashService</c> عند فشل provider)
    /// أو على عناصر محمية بقيود تبعية (مثل قيود مرتبطة بمناقلة صندوق).
    /// </summary>
    public bool CanPurge { get; set; } = true;

    /// <summary>سبب عدم إمكانية الحذف النهائي (يظهر كـ tooltip في الواجهة).</summary>
    public string? CannotPurgeReason { get; set; }
}
