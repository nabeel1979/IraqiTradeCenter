using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IraqiTradeCenterCompany.SharedKernel.Interfaces;

/// <summary>
/// تجريد مخزن المرفقات: يعزل الكود الأعلى (Application/Controllers) عن نوع
/// التخزين الفعلي (قرص محلي / Cloudflare R2 / Azure Blob …). كل تطبيق يُعيد
/// مفتاحاً (<c>storageKey</c>) يستخدمه التطبيق الأعلى لاحقاً عند القراءة والحذف.
/// التطبيق المحلي يخزّن داخل مجلد مهيّأ ضمن السيرفر، والتطبيق R2 يكتب إلى
/// Cloudflare R2 عبر S3 SDK — مفتاح التحويل في الإعدادات لا في الكود.
/// </summary>
public interface IAttachmentStorage
{
    /// <summary>
    /// اسم المخزن الحالي (يُسجَّل مع كل مرفق في <c>StorageProvider</c> لتسهيل
    /// المعرفة لاحقاً عند الهجرة بين مزوّدين).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// حفظ ملف وإعادة المفتاح الذي تستخدمه طبقة التطبيق لاسترجاعه.
    ///   <paramref name="logicalFolder"/> مجلد منطقي (مثلاً <c>"vouchers/123"</c>)
    ///   <paramref name="suggestedFileName"/> اسم الملف الأصلي (للحفاظ على الامتداد)
    /// </summary>
    Task<string> SaveAsync(string logicalFolder, string suggestedFileName, Stream content, string? contentType, CancellationToken ct = default);

    /// <summary>قراءة محتوى ملف بالـ <c>storageKey</c> (يرمي إن لم يوجد).</summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);

    /// <summary>حذف ملف (يُتجاهَل صامتاً لو لم يوجد).</summary>
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
