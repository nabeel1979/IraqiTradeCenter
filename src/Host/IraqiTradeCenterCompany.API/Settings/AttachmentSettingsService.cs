using System;
using System.Threading;
using System.Threading.Tasks;
using IraqiTradeCenterCompany.API.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IraqiTradeCenterCompany.API.Settings;

/// <summary>
/// مزوّد إعدادات مخزن المرفقات: يقرأ السطر الواحد من <c>auth.AttachmentStorageSettings</c>
/// مع تخزين مؤقت في الذاكرة (Process-wide singleton). أيّ تعديل من الواجهة يستدعي
/// <see cref="Invalidate"/> فتُعاد القراءة في الطلب التالي.
/// نتعمَّد <i>عدم</i> استخدام <c>IMemoryCache</c> هنا لأن الكاش بسيط جداً (مفتاح
/// واحد) ولأننا نحتاج تزامن قراءة/كتابة موثوق عبر الـ App Pool dependencies.
/// </summary>
public interface IAttachmentSettingsService
{
    Task<AttachmentStorageSettings> GetAsync(CancellationToken ct = default);
    Task<AttachmentStorageSettings> UpdateAsync(Action<AttachmentStorageSettings> mutate, string? updatedBy, CancellationToken ct = default);
    void Invalidate();
}

public class AttachmentSettingsService : IAttachmentSettingsService
{
    private readonly AuthDbContext _db;
    private readonly ILogger<AttachmentSettingsService> _log;
    private static AttachmentStorageSettings? _cached;
    private static readonly SemaphoreSlim _gate = new(1, 1);

    public AttachmentSettingsService(AuthDbContext db, ILogger<AttachmentSettingsService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<AttachmentStorageSettings> GetAsync(CancellationToken ct = default)
    {
        if (_cached != null) return _cached;
        await _gate.WaitAsync(ct);
        try
        {
            if (_cached != null) return _cached;
            var row = await _db.AttachmentStorageSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
            if (row == null)
            {
                // ‎البذرة: لا يوجد سطر بعد — ننشئه بالقيم الافتراضية حتى لا تظل
                // ‎الإعدادات معلَّقة عند أول طلب رفع.
                row = new AttachmentStorageSettings
                {
                    Id = 1,
                    Provider = "Local",
                    LocalRootPath = null,
                    MaxFileSizeBytes = 25L * 1024 * 1024,
                };
                _db.AttachmentStorageSettings.Add(row);
                try { await _db.SaveChangesAsync(ct); }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Seeding default AttachmentStorageSettings failed; falling back to in-memory defaults.");
                }
            }
            _cached = row;
            return row;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AttachmentStorageSettings> UpdateAsync(Action<AttachmentStorageSettings> mutate, string? updatedBy, CancellationToken ct = default)
    {
        var row = await _db.AttachmentStorageSettings.FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (row == null)
        {
            row = new AttachmentStorageSettings { Id = 1 };
            _db.AttachmentStorageSettings.Add(row);
        }
        mutate(row);
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedBy = updatedBy;
        await _db.SaveChangesAsync(ct);
        Invalidate();
        return await GetAsync(ct);
    }

    public void Invalidate() => _cached = null;
}
