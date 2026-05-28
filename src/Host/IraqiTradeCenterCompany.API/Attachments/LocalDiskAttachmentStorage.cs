using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IraqiTradeCenterCompany.API.Settings;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IraqiTradeCenterCompany.API.Attachments;

/// <summary>إعدادات مخزن المرفقات (تأتي من <c>appsettings.json</c> ⇒ <c>"Attachments"</c>).</summary>
public class AttachmentStorageOptions
{
    public const string SectionName = "Attachments";

    /// <summary>الافتراضي عند عدم وجود سطر في DB (Boot-strap قبل seed أول مرة).</summary>
    public string Provider { get; set; } = "Local";

    public LocalOptions Local { get; set; } = new();
    public R2Options R2 { get; set; } = new();

    public class LocalOptions
    {
        public string RootPath { get; set; } = "C:/ITC-Uploads/attachments";
    }

    public class R2Options
    {
        public string AccountId { get; set; } = "";
        public string AccessKeyId { get; set; } = "";
        public string SecretAccessKey { get; set; } = "";
        public string Bucket { get; set; } = "";
        public string PublicBaseUrl { get; set; } = "";
    }
}

/// <summary>
/// مخزن مرفقات محلي يكتب الملفات إلى قرص الخادم. <b>المسار يأتي ديناميكياً من
/// <see cref="IAttachmentSettingsService"/></b> (DB) — إن كان فارغاً نسقط إلى
/// قيمة <c>appsettings.json</c>.
///   • <see cref="OpenReadAsync"/> يُعيد <see cref="FileStream"/> مفتوحاً
///     (المُتلقّي مسؤول عن إغلاقه).
///   • <see cref="DeleteAsync"/> يتجاهل الملفات المفقودة بهدوء.
///   • <see cref="SaveAsync"/> ينشئ المجلد عند الحاجة، ويوّلد اسماً فريداً
///     لتجنّب التصادم: <c>{guid}_{safeOriginalName}</c>.
/// </summary>
public class LocalDiskAttachmentStorage : IAttachmentStorage
{
    private readonly IAttachmentSettingsService _settings;
    private readonly AttachmentStorageOptions _fallback;
    private readonly ILogger<LocalDiskAttachmentStorage> _log;

    public LocalDiskAttachmentStorage(
        IAttachmentSettingsService settings,
        IOptions<AttachmentStorageOptions> fallback,
        ILogger<LocalDiskAttachmentStorage> log)
    {
        _settings = settings;
        _fallback = fallback.Value;
        _log = log;
    }

    public string ProviderName => "Local";

    private async Task<string> ResolveRootAsync(CancellationToken ct)
    {
        var row = await _settings.GetAsync(ct);
        var root = string.IsNullOrWhiteSpace(row.LocalRootPath) ? _fallback.Local.RootPath : row.LocalRootPath!;
        try { Directory.CreateDirectory(root); }
        catch (Exception ex) { _log.LogWarning(ex, "Could not create attachments root {Root}", root); }
        return root;
    }

    public async Task<string> SaveAsync(string logicalFolder, string suggestedFileName, Stream content, string? contentType, CancellationToken ct = default)
    {
        var root = await ResolveRootAsync(ct);
        var safeFolder = SanitizeFolder(logicalFolder);
        var safeName = SanitizeFileName(suggestedFileName);
        var unique = $"{Guid.NewGuid():N}_{safeName}";
        var relKey = string.IsNullOrEmpty(safeFolder) ? unique : $"{safeFolder}/{unique}";
        var fullPath = Path.Combine(root, relKey.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);
        await content.CopyToAsync(fs, ct);
        return relKey;
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = await ResolveFullPathAsync(storageKey, ct);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"Attachment not found: {storageKey}");
        Stream s = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        return s;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            var fullPath = await ResolveFullPathAsync(storageKey, ct);
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to delete attachment {Key}", storageKey);
        }
    }

    /// <summary>
    /// يحوّل المفتاح النسبي إلى مسار فيزيائي ضمن جذر التخزين. يرفض المفاتيح
    /// التي تحاول الخروج من الجذر (path-traversal).
    /// </summary>
    private async Task<string> ResolveFullPathAsync(string storageKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("storageKey is empty");
        var root = await ResolveRootAsync(ct);
        var rel = storageKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, rel));
        var rootFull = Path.GetFullPath(root);
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Attachment key escapes storage root");
        return full;
    }

    private static string SanitizeFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return string.Empty;
        var clean = new string(folder.Select(c =>
            c == '/' || c == '_' || c == '-' || char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return clean.Replace("..", "_").Trim('/');
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var arr = (name ?? "file").Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var s = new string(arr).Trim();
        if (string.IsNullOrWhiteSpace(s)) s = "file";
        if (s.Length > 200) s = s[..200];
        return s;
    }
}
