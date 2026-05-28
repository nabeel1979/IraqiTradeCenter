using IraqiTradeCenterCompany.SharedKernel.Common;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

/// <summary>
/// أرشيف الوثائق المرفقة بسند أو قيد محاسبي:
///   • صورة الشيك / إيصال خارجي / مستند PDF / صورة الفاتورة الأصلية … إلخ.
///   • يقبل عدّة ملفات لنفس السند بتواريخ رفع متفرّقة (كل سطر له
///     <see cref="UploadedAtUtc"/> مستقل).
///   • التخزين الفيزيائي مفصول عبر <see cref="StorageProvider"/> +
///     <see cref="StorageKey"/>:
///       - <c>"Local"</c>  → المسار النسبي ضمن مجلد المرفقات على الخادم.
///       - <c>"R2"</c>     → مفتاح الكائن في Cloudflare R2 (للنشر الفعلي).
///     الواجهة الواحدة <c>IAttachmentStorage</c> تخفي الفرق فلا يلامس الكود
///     الأعلى نوع المخزن.
///   • <see cref="DisplayName"/> اسم بشري يُدخله المستخدم (مثلاً "شيك شركة س").
///   • <see cref="OriginalFileName"/> اسم الملف الأصلي عند الرفع (لتنزيله بنفس
///     الاسم وامتداده).
/// الحذف ناعم (<see cref="IsDeleted"/>) بحيث يبقى الملف على المخزن قابلاً للاستعادة
/// من سلّة المهملات الموحَّدة لاحقاً (إن أُضيف لها هذا النوع).
/// </summary>
public class VoucherAttachment : BaseEntity
{
    /// <summary>FK إلى القيد/السند الذي يخصّه المرفق.</summary>
    public int JournalEntryId { get; private set; }
    public virtual JournalEntry JournalEntry { get; private set; } = default!;

    /// <summary>اسم العرض الذي يُدخله المستخدم (مطلوب) — حتى 200 حرف.</summary>
    public string DisplayName { get; private set; } = default!;

    /// <summary>اسم الملف الأصلي (الذي رُفع)؛ يُستخدم لاسم التنزيل الافتراضي.</summary>
    public string OriginalFileName { get; private set; } = default!;

    /// <summary>"Local" أو "R2" حالياً — يحدّد كيف يُعاد بناء الملف عند القراءة.</summary>
    public string StorageProvider { get; private set; } = "Local";

    /// <summary>المفتاح الفريد للملف داخل المخزن (مسار نسبي محلي أو R2 key).</summary>
    public string StorageKey { get; private set; } = default!;

    /// <summary>نوع المحتوى (MIME) — يساعد المتصفح على عرض الملف بدل تنزيله.</summary>
    public string? ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    /// <summary>SHA-256 hex (اختياري) لاكتشاف التكرار/التلاعب لاحقاً.</summary>
    public string? Sha256 { get; private set; }

    public Guid? UploadedByUserId { get; private set; }
    public string? UploadedByUserName { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    /// <summary>ملاحظة اختيارية (سبب الرفع، مصدر الوثيقة، …).</summary>
    public string? Notes { get; private set; }

    private VoucherAttachment() { }

    public static VoucherAttachment Create(
        int journalEntryId,
        string displayName,
        string originalFileName,
        string storageProvider,
        string storageKey,
        long sizeBytes,
        string? contentType,
        string? sha256,
        Guid? uploadedByUserId,
        string? uploadedByUserName,
        string? notes)
    {
        return new VoucherAttachment
        {
            JournalEntryId = journalEntryId,
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? originalFileName
                : Truncate(displayName, 200),
            OriginalFileName = Truncate(originalFileName, 260),
            StorageProvider = Truncate(string.IsNullOrWhiteSpace(storageProvider) ? "Local" : storageProvider, 20),
            StorageKey = Truncate(storageKey, 500),
            SizeBytes = sizeBytes,
            ContentType = contentType is null ? null : Truncate(contentType, 150),
            Sha256 = sha256 is null ? null : Truncate(sha256, 64),
            UploadedByUserId = uploadedByUserId,
            UploadedByUserName = uploadedByUserName is null ? null : Truncate(uploadedByUserName, 150),
            UploadedAtUtc = DateTime.UtcNow,
            Notes = notes is null ? null : Truncate(notes, 500),
        };
    }

    /// <summary>إعادة تسمية المرفق (لا يمسّ الملف على المخزن).</summary>
    public void Rename(string newDisplayName)
    {
        if (string.IsNullOrWhiteSpace(newDisplayName)) return;
        DisplayName = Truncate(newDisplayName, 200);
    }

    /// <summary>تحديث ملاحظات المرفق.</summary>
    public void UpdateNotes(string? newNotes)
    {
        Notes = string.IsNullOrWhiteSpace(newNotes) ? null : Truncate(newNotes.Trim(), 500);
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
