using System;
using System.Threading;
using System.Threading.Tasks;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IraqiTradeCenterCompany.API.Auth.Auditing;

/// <summary>
/// تطبيق <see cref="IraqiTradeCenterCompany.SharedKernel.Interfaces.IAuditLogger"/>
/// في طبقة Host (يستعمل <see cref="AuthDbContext"/> و <see cref="HttpContext"/>).
/// الأخطاء أثناء التسجيل لا تُفشل العملية الأصلية — تُسجَّل كتحذير فقط.
/// </summary>
public class AuditLogger : IraqiTradeCenterCompany.SharedKernel.Interfaces.IAuditLogger
{
    private readonly AuthDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpCtx;
    private readonly ILogger<AuditLogger> _log;

    public AuditLogger(
        AuthDbContext db,
        ICurrentUserService currentUser,
        IHttpContextAccessor httpCtx,
        ILogger<AuditLogger> log)
    {
        _db = db;
        _currentUser = currentUser;
        _httpCtx = httpCtx;
        _log = log;
    }

    public async Task LogAsync(
        string entityType,
        string entityId,
        string action,
        string? summary = null,
        object? details = null,
        CancellationToken ct = default)
    {
        try
        {
            var entry = new AuditLog
            {
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Summary = Truncate(summary, 400),
                DetailsJson = details is null
                    ? null
                    : System.Text.Json.JsonSerializer.Serialize(details, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = false,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    }),
                UserId = _currentUser.UserId,
                UserName = _currentUser.FullName,
                IpAddress = ResolveIp(),
                UserAgent = Truncate(_httpCtx.HttpContext?.Request.Headers["User-Agent"].ToString(), 300),
                OccurredAtUtc = DateTime.UtcNow,
            };
            _db.Set<AuditLog>().Add(entry);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // ‎لا نُفشل العملية الأصلية لو فشل التسجيل — هذا سجل تكميلي.
            _log.LogWarning(ex, "AuditLogger failed for {EntityType} {EntityId} {Action}",
                entityType, entityId, action);
        }
    }

    private string? ResolveIp()
    {
        var ctx = _httpCtx.HttpContext;
        if (ctx == null) return null;
        // ‎احترام رؤوس البروكسي المعتادة (X-Forwarded-For) — أول قيمة هي الأصل.
        var xff = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(xff))
        {
            var first = xff.Split(',', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            return Truncate(first, 64);
        }
        return Truncate(ctx.Connection.RemoteIpAddress?.ToString(), 64);
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        return value.Length <= max ? value : value[..max];
    }
}
