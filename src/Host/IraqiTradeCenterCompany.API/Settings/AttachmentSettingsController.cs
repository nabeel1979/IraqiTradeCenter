using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.API.Controllers;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Settings;

/// <summary>
/// إعدادات مخزن المرفقات (المسار المحلي + مفاتيح R2) قابلة للتعديل من واجهة
/// النظام دون إعادة نشر. تتطلَّب صلاحية <c>System.CompanySettings.Update</c>.
///
/// الـ GET لا يُعيد الـ Secret الكامل (يُقنَّع بنجوم) — يكفي إظهار آخر 4
/// محارف لتأكيد الحفظ. الـ PUT يقبل قيمة فارغة لإبقاء الـ Secret القديم.
/// </summary>
[Route("api/settings/attachments")]
public class AttachmentSettingsController : BaseApiController
{
    private readonly IAttachmentSettingsService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _perms;
    private readonly IAuditLogger _audit;

    public AttachmentSettingsController(
        IAttachmentSettingsService service,
        ICurrentUserService currentUser,
        IPermissionService perms,
        IAuditLogger audit)
    {
        _service = service;
        _currentUser = currentUser;
        _perms = perms;
        _audit = audit;
    }

    public class AttachmentSettingsDto
    {
        public string Provider { get; set; } = "Local";
        public string? LocalRootPath { get; set; }
        public string? R2AccountId { get; set; }
        public string? R2AccessKeyId { get; set; }
        /// <summary>المفتاح السرّي مُقنَّع — يَظهر فقط آخر 4 محارف على شكل <c>****abcd</c>.</summary>
        public string? R2SecretAccessKeyMasked { get; set; }
        /// <summary>هل المفتاح السرّي مُعيَّن فعلياً (لتمييز "غير مُعيَّن" عن "تمّ حفظه").</summary>
        public bool R2SecretAccessKeySet { get; set; }
        public string? R2Bucket { get; set; }
        public string? R2PublicBaseUrl { get; set; }
        public long MaxFileSizeBytes { get; set; }
        public string? UpdatedAtUtc { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class UpdateAttachmentSettingsRequest
    {
        public string? Provider { get; set; }
        public string? LocalRootPath { get; set; }
        public string? R2AccountId { get; set; }
        public string? R2AccessKeyId { get; set; }
        /// <summary>إن كان <c>null</c> أو فارغاً نُبقي السرّ القديم؛ غير ذلك نستبدله.</summary>
        public string? R2SecretAccessKey { get; set; }
        public string? R2Bucket { get; set; }
        public string? R2PublicBaseUrl { get; set; }
        public long? MaxFileSizeBytes { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!await CanReadAsync(ct)) return Forbid();
        var row = await _service.GetAsync(ct);
        return Ok(new { success = true, data = ToDto(row) });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateAttachmentSettingsRequest req, CancellationToken ct)
    {
        if (!await CanWriteAsync(ct)) return Forbid();
        if (req == null) return BadRequest(new { success = false, message = "Empty payload" });

        // ‎تحقّق سريع: لو provider=Local يجب أن يكون المسار قابلاً للكتابة (نتحقق
        // ‎بمحاولة إنشائه عند الحفظ — هنا فقط نمنع القيم الفارغة الواضحة).
        var newProvider = string.IsNullOrWhiteSpace(req.Provider) ? null : req.Provider!.Trim();
        if (newProvider != null
            && !string.Equals(newProvider, "Local", System.StringComparison.OrdinalIgnoreCase)
            && !string.Equals(newProvider, "R2", System.StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "Provider must be 'Local' or 'R2'." });
        }

        var by = _currentUser.FullName ?? _currentUser.UserId?.ToString();
        var saved = await _service.UpdateAsync(row =>
        {
            if (newProvider != null) row.Provider = newProvider;
            if (req.LocalRootPath != null) row.LocalRootPath = string.IsNullOrWhiteSpace(req.LocalRootPath) ? null : req.LocalRootPath.Trim();
            if (req.R2AccountId != null) row.R2AccountId = NullIfEmpty(req.R2AccountId);
            if (req.R2AccessKeyId != null) row.R2AccessKeyId = NullIfEmpty(req.R2AccessKeyId);
            // ‎السرّ: فارغ ⇒ أبقِ القديم. غير فارغ ⇒ استبدل.
            if (!string.IsNullOrWhiteSpace(req.R2SecretAccessKey)) row.R2SecretAccessKey = req.R2SecretAccessKey!.Trim();
            if (req.R2Bucket != null) row.R2Bucket = NullIfEmpty(req.R2Bucket);
            if (req.R2PublicBaseUrl != null) row.R2PublicBaseUrl = NullIfEmpty(req.R2PublicBaseUrl);
            if (req.MaxFileSizeBytes.HasValue && req.MaxFileSizeBytes.Value > 0) row.MaxFileSizeBytes = req.MaxFileSizeBytes.Value;
        }, by, ct);

        // ‎للمزوّد المحلي: حاول إنشاء المجلد كي يفشل الحفظ مبكراً إن لم يكن متاحاً.
        if (string.Equals(saved.Provider, "Local", System.StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(saved.LocalRootPath))
        {
            try { Directory.CreateDirectory(saved.LocalRootPath!); }
            catch (System.Exception ex)
            {
                return Ok(new
                {
                    success = true,
                    warning = $"تم الحفظ لكن لم نتمكن من إنشاء المجلد المحلي: {ex.Message}",
                    data = ToDto(saved),
                });
            }
        }

        await _audit.LogAsync(
            entityType: "AttachmentStorageSettings",
            entityId: "1",
            action: AuditActions.Update,
            summary: "تحديث إعدادات مخزن المرفقات",
            details: new
            {
                provider = saved.Provider,
                hasLocalRoot = !string.IsNullOrWhiteSpace(saved.LocalRootPath),
                hasR2 = !string.IsNullOrWhiteSpace(saved.R2Bucket),
                maxFileSizeBytes = saved.MaxFileSizeBytes,
            },
            ct: ct);

        return Ok(new { success = true, data = ToDto(saved) });
    }

    private async Task<bool> CanReadAsync(CancellationToken ct)
    {
        if (_currentUser.IsSuperAdmin) return true;
        var uid = _currentUser.UserId;
        if (uid is null) return false;
        return await _perms.HasPermissionAsync(uid.Value, PermissionRegistry.System.CompanySettings.Read, ct)
            || await _perms.HasPermissionAsync(uid.Value, PermissionRegistry.System.CompanySettings.Update, ct);
    }

    private async Task<bool> CanWriteAsync(CancellationToken ct)
    {
        if (_currentUser.IsSuperAdmin) return true;
        var uid = _currentUser.UserId;
        if (uid is null) return false;
        return await _perms.HasPermissionAsync(uid.Value, PermissionRegistry.System.CompanySettings.Update, ct);
    }

    private static AttachmentSettingsDto ToDto(AttachmentStorageSettings row) => new()
    {
        Provider = row.Provider,
        LocalRootPath = row.LocalRootPath,
        R2AccountId = row.R2AccountId,
        R2AccessKeyId = row.R2AccessKeyId,
        R2SecretAccessKeyMasked = Mask(row.R2SecretAccessKey),
        R2SecretAccessKeySet = !string.IsNullOrWhiteSpace(row.R2SecretAccessKey),
        R2Bucket = row.R2Bucket,
        R2PublicBaseUrl = row.R2PublicBaseUrl,
        MaxFileSizeBytes = row.MaxFileSizeBytes,
        UpdatedAtUtc = row.UpdatedAtUtc?.ToString("o"),
        UpdatedBy = row.UpdatedBy,
    };

    private static string? Mask(string? secret)
    {
        if (string.IsNullOrEmpty(secret)) return null;
        if (secret.Length <= 4) return new string('*', secret.Length);
        return new string('*', System.Math.Min(secret.Length - 4, 12)) + secret[^4..];
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
