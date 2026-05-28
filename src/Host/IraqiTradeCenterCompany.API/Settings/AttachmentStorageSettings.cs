using System;

namespace IraqiTradeCenterCompany.API.Settings;

/// <summary>
/// إعدادات مخزن المرفقات (Voucher Attachments) القابلة للتعديل من الواجهة دون
/// إعادة نشر. سطر واحد فقط (Singleton) معرّفه دائماً <c>1</c>:
///   • <see cref="Provider"/>: <c>"Local"</c> ⇒ يُكتب في قرص الخادم.
///                            <c>"R2"</c>    ⇒ يُكتب في Cloudflare R2 (يحتاج تكامل
///                            <c>AWSSDK.S3</c> داخل <c>R2AttachmentStorage</c>).
///   • <see cref="LocalRootPath"/>: المسار الفعلي على الخادم عند Provider=Local.
///     يجب أن يكون متاح الكتابة لحساب الـ App Pool. لو فارغ نستعمل الافتراضي
///     من <c>appsettings.json</c> (<c>Attachments:Local:RootPath</c>).
///   • حقول R2 تُخزَّن كنص — مفاتيح Cloudflare R2 لا تُعاد للواجهة (تُقنَّع
///     في الاستجابة) لكنها تبقى مقروءة من جانب الخادم.
/// </summary>
public class AttachmentStorageSettings
{
    public int Id { get; set; } = 1;

    /// <summary>"Local" أو "R2".</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>المسار الكامل على الخادم — يُقرأ فقط عندما <see cref="Provider"/> = "Local".</summary>
    public string? LocalRootPath { get; set; }

    /// <summary>R2 Account Id (يظهر في لوحة Cloudflare).</summary>
    public string? R2AccountId { get; set; }

    /// <summary>R2 Access Key Id.</summary>
    public string? R2AccessKeyId { get; set; }

    /// <summary>R2 Secret Access Key (لا يُعاد للواجهة بعد الحفظ — يُقنَّع).</summary>
    public string? R2SecretAccessKey { get; set; }

    public string? R2Bucket { get; set; }

    /// <summary>اختياري: عنوان عام أمام R2 (Worker / Custom Domain) — لاستعمال روابط مباشرة لاحقاً.</summary>
    public string? R2PublicBaseUrl { get; set; }

    /// <summary>أكبر حجم مسموح للملف الواحد (بالبايت). افتراضياً 25MB.</summary>
    public long MaxFileSizeBytes { get; set; } = 25L * 1024 * 1024;

    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
