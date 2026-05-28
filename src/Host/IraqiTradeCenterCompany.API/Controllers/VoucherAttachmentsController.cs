using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using IraqiTradeCenterCompany.API.Attachments;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.API.Settings;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Controllers;

/// <summary>
/// أرشيف المرفقات لكل سند/قيد محاسبي.
/// المسارات:
///   • <c>GET    /api/vouchers/{entryId}/attachments</c>
///   • <c>POST   /api/vouchers/{entryId}/attachments</c>  (multipart: file + displayName)
///   • <c>GET    /api/vouchers/{entryId}/attachments/{attId}/download</c>
///   • <c>DELETE /api/vouchers/{entryId}/attachments/{attId}</c>
///
/// الحماية: قراءة المرفقات تتطلب صلاحية قراءة القيود اليومية (أو قراءة نوع السند
/// المحدّد إن كان مرتبطاً). الرفع/الحذف يتطلبان <c>Accounting.JournalEntries.Update</c>
/// أو ما يقابلها لنوع السند المخصّص — لأن إضافة مرفق تُعتبر تعديلاً على هيدر السند.
/// </summary>
[Route("api/vouchers/{entryId:int}/attachments")]
public class VoucherAttachmentsController : BaseApiController
{
    private readonly IAccountingDbContext _db;
    private readonly IAttachmentStorageRegistry _storageRegistry;
    private readonly IAttachmentSettingsService _attachmentSettings;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _perms;

    public VoucherAttachmentsController(
        IAccountingDbContext db,
        IAttachmentStorageRegistry storageRegistry,
        IAttachmentSettingsService attachmentSettings,
        IAuditLogger audit,
        ICurrentUserService currentUser,
        IPermissionService perms)
    {
        _db = db;
        _storageRegistry = storageRegistry;
        _attachmentSettings = attachmentSettings;
        _audit = audit;
        _currentUser = currentUser;
        _perms = perms;
    }

    public class AttachmentDto
    {
        public long Id { get; set; }
        public int JournalEntryId { get; set; }
        public string DisplayName { get; set; } = default!;
        public string OriginalFileName { get; set; } = default!;
        public string? ContentType { get; set; }
        public long SizeBytes { get; set; }
        public string StorageProvider { get; set; } = default!;
        public Guid? UploadedByUserId { get; set; }
        public string? UploadedByUserName { get; set; }
        public DateTime UploadedAtUtc { get; set; }
        public string? Notes { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> List(int entryId, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry == null) return NotFound(new { success = false, message = "القيد غير موجود" });

        if (!await CanReadAsync(entry, ct))
            return Forbid();

        var rows = await _db.VoucherAttachments.AsNoTracking()
            .Where(a => a.JournalEntryId == entryId && !a.IsDeleted)
            .OrderByDescending(a => a.UploadedAtUtc).ThenByDescending(a => a.Id)
            .Select(a => new AttachmentDto
            {
                Id = a.Id,
                JournalEntryId = a.JournalEntryId,
                DisplayName = a.DisplayName,
                OriginalFileName = a.OriginalFileName,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes,
                StorageProvider = a.StorageProvider,
                UploadedByUserId = a.UploadedByUserId,
                UploadedByUserName = a.UploadedByUserName,
                UploadedAtUtc = a.UploadedAtUtc,
                Notes = a.Notes,
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = rows });
    }

    [HttpPost]
    [RequestSizeLimit(256L * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        int entryId,
        [FromForm] IFormFile? file,
        [FromForm] string? displayName,
        [FromForm] string? notes,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "لم يُرفع أي ملف" });

        var settingsRow = await _attachmentSettings.GetAsync(ct);
        var maxBytes = settingsRow.MaxFileSizeBytes > 0 ? settingsRow.MaxFileSizeBytes : 25L * 1024 * 1024;
        if (file.Length > maxBytes)
            return BadRequest(new { success = false, message = $"حجم الملف يتجاوز الحد الأعلى ({maxBytes / (1024 * 1024)} ميجابايت)" });

        var entry = await _db.JournalEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry == null) return NotFound(new { success = false, message = "القيد غير موجود" });

        if (!await CanModifyAsync(entry, ct))
            return Forbid();

        var storage = await _storageRegistry.CurrentAsync(ct);

        // ‎احفظ على المخزن مع حساب SHA-256 خفّة لكشف التكرار لاحقاً.
        string sha256;
        long size;
        string storageKey;
        var tempPath = Path.GetTempFileName();
        try
        {
            // ‎نُمرّر النسخة على القرص المؤقت إلى المخزن النهائي حتى لا يحتاج
            // ‎المخزن إلى buffering كامل في الذاكرة. ASP.NET قد يكون قد قرأ
            // ‎الـ stream إلى ملف مؤقّت بالفعل لو تجاوز buffer الذاكرة.
            await using (var temp = System.IO.File.Create(tempPath))
            {
                await file.CopyToAsync(temp, ct);
            }
            using (var sha = SHA256.Create())
            await using (var fs = System.IO.File.OpenRead(tempPath))
            {
                var hash = await sha.ComputeHashAsync(fs, ct);
                sha256 = Convert.ToHexString(hash).ToLowerInvariant();
                size = fs.Length;
            }
            await using (var fs = System.IO.File.OpenRead(tempPath))
            {
                storageKey = await storage.SaveAsync(
                    logicalFolder: $"vouchers/{entryId}",
                    suggestedFileName: file.FileName,
                    content: fs,
                    contentType: file.ContentType,
                    ct: ct);
            }
        }
        finally
        {
            try { if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); } catch { }
        }

        var attachment = VoucherAttachment.Create(
            journalEntryId: entryId,
            displayName: string.IsNullOrWhiteSpace(displayName) ? file.FileName : displayName!,
            originalFileName: file.FileName,
            storageProvider: storage.ProviderName,
            storageKey: storageKey,
            sizeBytes: size,
            contentType: file.ContentType,
            sha256: sha256,
            uploadedByUserId: _currentUser.UserId,
            uploadedByUserName: _currentUser.FullName,
            notes: notes);

        _db.VoucherAttachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            entityType: "VoucherAttachment",
            entityId: attachment.Id.ToString(),
            action: AuditActions.Create,
            summary: $"إضافة مرفق '{attachment.DisplayName}' للسند #{entry.VoucherSequence?.ToString() ?? entry.EntryNumber}",
            details: new
            {
                voucherId = entryId,
                voucherTypeId = entry.VoucherTypeId,
                voucherSequence = entry.VoucherSequence,
                fileName = attachment.OriginalFileName,
                size,
                provider = attachment.StorageProvider,
            },
            ct: ct);

        // ‎سجّل أيضاً في سجل السند حتى تظهر الحركة عند فتح مراقبة السند.
        await _audit.LogAsync(
            entityType: entry.VoucherTypeId.HasValue ? "Voucher" : "JournalEntry",
            entityId: entryId.ToString(),
            action: AuditActions.Update,
            summary: $"إضافة مرفق: '{attachment.DisplayName}'",
            details: new { attachmentId = attachment.Id, fileName = attachment.OriginalFileName, size },
            ct: ct);

        return Ok(new
        {
            success = true,
            data = new AttachmentDto
            {
                Id = attachment.Id,
                JournalEntryId = attachment.JournalEntryId,
                DisplayName = attachment.DisplayName,
                OriginalFileName = attachment.OriginalFileName,
                ContentType = attachment.ContentType,
                SizeBytes = attachment.SizeBytes,
                StorageProvider = attachment.StorageProvider,
                UploadedByUserId = attachment.UploadedByUserId,
                UploadedByUserName = attachment.UploadedByUserName,
                UploadedAtUtc = attachment.UploadedAtUtc,
                Notes = attachment.Notes,
            },
        });
    }

    [HttpGet("{attId:long}/download")]
    public async Task<IActionResult> Download(int entryId, long attId, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry == null) return NotFound();
        if (!await CanReadAsync(entry, ct)) return Forbid();

        var att = await _db.VoucherAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attId && a.JournalEntryId == entryId && !a.IsDeleted, ct);
        if (att == null) return NotFound();

        // ‎سجل قراءة (Print/View) على المرفق — حدث مفيد للتدقيق ("من نزّل الوثيقة؟").
        await _audit.LogAsync(
            entityType: "VoucherAttachment",
            entityId: att.Id.ToString(),
            action: AuditActions.View,
            summary: $"تنزيل مرفق '{att.DisplayName}' للسند #{entry.VoucherSequence?.ToString() ?? entry.EntryNumber}",
            details: new { voucherId = entryId, fileName = att.OriginalFileName },
            ct: ct);

        // ‎اقرأ من نفس المخزن الذي حُفظ به سابقاً (قد يختلف عن الإعداد الحالي).
        var readStorage = _storageRegistry.GetByName(att.StorageProvider);
        var stream = await readStorage.OpenReadAsync(att.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(att.ContentType) ? "application/octet-stream" : att.ContentType!;
        return File(stream, contentType, fileDownloadName: att.OriginalFileName, enableRangeProcessing: true);
    }

    // ── طلب تغيير اسم العرض ──────────────────────────────────────────────
    public record RenameRequest(string DisplayName);

    [HttpPatch("{attId:long}/rename")]
    public async Task<IActionResult> Rename(int entryId, long attId, [FromBody] RenameRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body?.DisplayName))
            return BadRequest(new { success = false, message = "اسم العرض لا يمكن أن يكون فارغاً" });

        var entry = await _db.JournalEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry == null) return NotFound(new { success = false, message = "القيد غير موجود" });
        if (!await CanModifyAsync(entry, ct)) return Forbid();

        var att = await _db.VoucherAttachments
            .FirstOrDefaultAsync(a => a.Id == attId && a.JournalEntryId == entryId && !a.IsDeleted, ct);
        if (att == null) return NotFound(new { success = false, message = "المرفق غير موجود" });

        var oldName = att.DisplayName;
        att.Rename(body.DisplayName.Trim());
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            entityType: "VoucherAttachment",
            entityId: att.Id.ToString(),
            action: AuditActions.Update,
            summary: $"تغيير اسم المرفق من '{oldName}' إلى '{att.DisplayName}' في السند #{entry.VoucherSequence?.ToString() ?? entry.EntryNumber}",
            details: new { voucherId = entryId, oldName, newName = att.DisplayName },
            ct: ct);

        // ‎سجّل أيضاً في سجل السند الأصلي حتى تظهر الحركة عند فتح مراقبة السند.
        await _audit.LogAsync(
            entityType: entry.VoucherTypeId.HasValue ? "Voucher" : "JournalEntry",
            entityId: entryId.ToString(),
            action: AuditActions.Update,
            summary: $"تغيير اسم مرفق: من '{oldName}' إلى '{att.DisplayName}'",
            details: new { attachmentId = att.Id, oldName, newName = att.DisplayName },
            ct: ct);

        return Ok(new
        {
            success = true,
            data = new AttachmentDto
            {
                Id = att.Id,
                JournalEntryId = att.JournalEntryId,
                DisplayName = att.DisplayName,
                OriginalFileName = att.OriginalFileName,
                ContentType = att.ContentType,
                SizeBytes = att.SizeBytes,
                StorageProvider = att.StorageProvider,
                UploadedByUserId = att.UploadedByUserId,
                UploadedByUserName = att.UploadedByUserName,
                UploadedAtUtc = att.UploadedAtUtc,
                Notes = att.Notes,
            },
        });
    }

    // ── تحديث ملاحظات المرفق ──────────────────────────────────────────────
    public record UpdateNotesRequest(string? Notes);

    [HttpPatch("{attId:long}/notes")]
    public async Task<IActionResult> UpdateNotes(int entryId, long attId, [FromBody] UpdateNotesRequest body, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry == null) return NotFound(new { success = false, message = "القيد غير موجود" });
        if (!await CanModifyAsync(entry, ct)) return Forbid();

        var att = await _db.VoucherAttachments
            .FirstOrDefaultAsync(a => a.Id == attId && a.JournalEntryId == entryId && !a.IsDeleted, ct);
        if (att == null) return NotFound(new { success = false, message = "المرفق غير موجود" });

        var oldNotes = att.Notes;
        att.UpdateNotes(body?.Notes);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            entityType: entry.VoucherTypeId.HasValue ? "Voucher" : "JournalEntry",
            entityId: entryId.ToString(),
            action: AuditActions.Update,
            summary: $"تحديث ملاحظات مرفق '{att.DisplayName}'",
            details: new { attachmentId = att.Id, oldNotes, newNotes = att.Notes },
            ct: ct);

        return Ok(new
        {
            success = true,
            data = new AttachmentDto
            {
                Id = att.Id,
                JournalEntryId = att.JournalEntryId,
                DisplayName = att.DisplayName,
                OriginalFileName = att.OriginalFileName,
                ContentType = att.ContentType,
                SizeBytes = att.SizeBytes,
                StorageProvider = att.StorageProvider,
                UploadedByUserId = att.UploadedByUserId,
                UploadedByUserName = att.UploadedByUserName,
                UploadedAtUtc = att.UploadedAtUtc,
                Notes = att.Notes,
            },
        });
    }

    [HttpDelete("{attId:long}")]
    public async Task<IActionResult> Delete(int entryId, long attId, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry == null) return NotFound(new { success = false, message = "القيد غير موجود" });
        if (!await CanModifyAsync(entry, ct)) return Forbid();

        var att = await _db.VoucherAttachments
            .FirstOrDefaultAsync(a => a.Id == attId && a.JournalEntryId == entryId, ct);
        if (att == null) return NotFound(new { success = false, message = "المرفق غير موجود" });

        // ‎احذف الملف من المخزن أولاً ثم الصفّ — لو فشل الحذف من المخزن نكتفي بـ
        // ‎soft-delete على الـ DB حتى لا نعمي السجل أن المستخدم حاول الحذف.
        try
        {
            var delStorage = _storageRegistry.GetByName(att.StorageProvider);
            await delStorage.DeleteAsync(att.StorageKey, ct);
        }
        catch { /* تُسجَّل لكن لا تُفشل العملية */ }

        att.MarkAsDeleted();
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            entityType: "VoucherAttachment",
            entityId: att.Id.ToString(),
            action: AuditActions.Delete,
            summary: $"حذف مرفق '{att.DisplayName}' من السند #{entry.VoucherSequence?.ToString() ?? entry.EntryNumber}",
            details: new { voucherId = entryId, fileName = att.OriginalFileName },
            ct: ct);

        // ‎سجّل أيضاً في سجل السند حتى تظهر الحركة عند فتح مراقبة السند.
        await _audit.LogAsync(
            entityType: entry.VoucherTypeId.HasValue ? "Voucher" : "JournalEntry",
            entityId: entryId.ToString(),
            action: AuditActions.Delete,
            summary: $"حذف مرفق: '{att.DisplayName}'",
            details: new { attachmentId = att.Id, fileName = att.OriginalFileName },
            ct: ct);

        return Ok(new { success = true });
    }

    // ─── Helpers: تحديد الصلاحيات ────────────────────────────────────────
    /// <summary>قراءة الأرشيف = قراءة القيد المرتبط (لكل من الأنواع).</summary>
    private async Task<bool> CanReadAsync(JournalEntry entry, CancellationToken ct)
    {
        if (_currentUser.IsSuperAdmin) return true;
        var uid = _currentUser.UserId;
        if (uid is null) return false;

        // ‎قيد مرتبط بنوع سند → استخدم صلاحية ذلك النوع
        if (entry.VoucherTypeId.HasValue)
        {
            var code = await _db.JournalVoucherTypes.AsNoTracking()
                .Where(v => v.Id == entry.VoucherTypeId.Value)
                .Select(v => v.Code)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrEmpty(code))
            {
                var permRead = $"Accounting.Vouchers.{code.ToUpperInvariant()}.Read";
                if (await _perms.HasPermissionAsync(uid.Value, permRead, ct)) return true;
            }
        }
        return await _perms.HasPermissionAsync(uid.Value, PermissionRegistry.Accounting.JournalEntries.Read, ct);
    }

    /// <summary>تعديل الأرشيف (إضافة/حذف) = تعديل القيد المرتبط.</summary>
    private async Task<bool> CanModifyAsync(JournalEntry entry, CancellationToken ct)
    {
        if (_currentUser.IsSuperAdmin) return true;
        var uid = _currentUser.UserId;
        if (uid is null) return false;

        if (entry.VoucherTypeId.HasValue)
        {
            var code = await _db.JournalVoucherTypes.AsNoTracking()
                .Where(v => v.Id == entry.VoucherTypeId.Value)
                .Select(v => v.Code)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrEmpty(code))
            {
                var permUpd = $"Accounting.Vouchers.{code.ToUpperInvariant()}.Update";
                if (await _perms.HasPermissionAsync(uid.Value, permUpd, ct)) return true;
            }
        }
        return await _perms.HasPermissionAsync(uid.Value, PermissionRegistry.Accounting.JournalEntries.Update, ct);
    }
}
