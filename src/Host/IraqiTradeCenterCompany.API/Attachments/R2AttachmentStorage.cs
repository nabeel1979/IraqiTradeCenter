using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IraqiTradeCenterCompany.API.Settings;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.Extensions.Logging;

namespace IraqiTradeCenterCompany.API.Attachments;

/// <summary>
/// مخزن مرفقات يكتب إلى Cloudflare R2 عبر بروتوكول S3-متوافق. الإعدادات تأتي
/// ديناميكياً من <see cref="IAttachmentSettingsService"/> فلا يحتاج المستخدم
/// تعديل <c>appsettings.json</c>: يكفي حفظ مفاتيح R2 من صفحة الإعدادات.
///
/// <para>
/// <b>للتفعيل الفعلي</b>: أضف الحزمة <c>AWSSDK.S3</c> ثم استبدل أجسام
/// <c>SaveAsync</c>/<c>OpenReadAsync</c>/<c>DeleteAsync</c> باستدعاءات S3 client
/// موجِّهاً الـ <c>ServiceURL</c> إلى:
/// <c>https://{AccountId}.r2.cloudflarestorage.com</c> مع <c>ForcePathStyle=true</c>
/// و <c>SignatureVersion="4"</c>.
/// </para>
///
/// حالياً يرمي <see cref="NotImplementedException"/> ليُنبّه المطوّر أن التفعيل
/// = تركيب المكتبة + ملء الـ secrets في صفحة الإعدادات.
/// </summary>
public class R2AttachmentStorage : IAttachmentStorage
{
    private readonly IAttachmentSettingsService _settings;
    private readonly ILogger<R2AttachmentStorage> _log;

    public R2AttachmentStorage(
        IAttachmentSettingsService settings,
        ILogger<R2AttachmentStorage> log)
    {
        _settings = settings;
        _log = log;
    }

    public string ProviderName => "R2";

    public Task<string> SaveAsync(string logicalFolder, string suggestedFileName, Stream content, string? contentType, CancellationToken ct = default)
    {
        _ = _settings.GetAsync(ct);
        // TODO: استبدل بـ AWSSDK.S3 PutObjectAsync إلى R2 باستخدام row.R2AccountId/...
        throw new NotImplementedException("R2 storage not wired yet — install AWSSDK.S3 and implement using settings from DB.");
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        throw new NotImplementedException("R2 storage not wired yet — install AWSSDK.S3 and implement.");
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        throw new NotImplementedException("R2 storage not wired yet — install AWSSDK.S3 and implement.");
    }
}
