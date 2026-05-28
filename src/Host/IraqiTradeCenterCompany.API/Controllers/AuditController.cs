using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.API.Auth.Auditing;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Controllers;

/// <summary>
/// واجهة سجل المراقبة (Audit Log).
///   • <c>GET  /api/audit</c>                  — قائمة مفلتَرة (تاريخ، مستخدم، عملية، كيان)
///   • <c>GET  /api/audit/entity/{type}/{id}</c> — تاريخ كيان محدّد (للأيقونة في السند/القيد)
///   • <c>POST /api/audit/print</c>            — يستقبل حدث طباعة من العميل لتسجيله
///
/// الحماية: قراءة السجل تتطلب <see cref="PermissionRegistry.System.Audit.Read"/>.
/// تسجيل أحداث الطباعة لا يحتاج صلاحية إضافية لأن العميل لا يستطيع تسجيل سوى
/// طباعة كيان موجود وضمن صلاحياته (الواجهة لا تُظهر زر الطباعة بدون ذلك).
/// </summary>
public class AuditController : BaseApiController
{
    private readonly AuthDbContext _db;
    private readonly IAuditLogger _logger;

    public AuditController(AuthDbContext db, IAuditLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(PermissionRegistry.System.Audit.Read)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? action = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 500) pageSize = 50;

        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(x => x.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId))   q = q.Where(x => x.EntityId == entityId);
        if (!string.IsNullOrWhiteSpace(action))     q = q.Where(x => x.Action == action);
        if (userId.HasValue)                        q = q.Where(x => x.UserId == userId.Value);
        if (fromUtc.HasValue)                       q = q.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)                         q = q.Where(x => x.OccurredAtUtc <= toUtc.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x =>
                (x.Summary != null && x.Summary.Contains(s)) ||
                (x.UserName != null && x.UserName.Contains(s)) ||
                x.EntityId.Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Action = x.Action,
                Summary = x.Summary,
                DetailsJson = x.DetailsJson,
                UserId = x.UserId,
                UserName = x.UserName,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                OccurredAtUtc = x.OccurredAtUtc,
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                items,
                totalCount = total,
                pageNumber,
                pageSize,
            },
        });
    }

    /// <summary>
    /// تاريخ كيان محدد (يُستدعى من أيقونة "مراقبة" داخل كرت السند/القيد).
    /// لا يحتاج صفحات لأن سجل كيان واحد يندر أن يتجاوز عشرات الإدخالات.
    /// </summary>
    [HttpGet("entity/{type}/{id}")]
    [RequirePermission(PermissionRegistry.System.Audit.Read)]
    public async Task<IActionResult> ByEntity(string type, string id, CancellationToken ct)
    {
        var items = await _db.AuditLogs.AsNoTracking()
            .Where(x => x.EntityType == type && x.EntityId == id)
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Action = x.Action,
                Summary = x.Summary,
                DetailsJson = x.DetailsJson,
                UserId = x.UserId,
                UserName = x.UserName,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                OccurredAtUtc = x.OccurredAtUtc,
            })
            .ToListAsync(ct);
        return Ok(new { success = true, data = items });
    }

    /// <summary>
    /// نقطة استقبال أحداث الطباعة من العميل (Front-end). نسجِّل أن المستخدم
    /// طبع/صدّر كياناً معيناً. لا توجد بصمة على القيد نفسه — فقط سطر في سجل
    /// المراقبة.
    /// </summary>
    [HttpPost("print")]
    public async Task<IActionResult> LogPrint([FromBody] LogPrintRequest req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.EntityType) || string.IsNullOrWhiteSpace(req.EntityId))
            return BadRequest(new { success = false, message = "entityType/entityId مطلوبان" });
        await _logger.LogAsync(
            entityType: req.EntityType,
            entityId: req.EntityId,
            action: AuditActions.Print,
            summary: req.Summary,
            details: req.Details,
            ct: ct);
        return Ok(new { success = true });
    }

    public class LogPrintRequest
    {
        public string EntityType { get; set; } = default!;
        public string EntityId { get; set; } = default!;
        public string? Summary { get; set; }
        public object? Details { get; set; }
    }

    public class AuditLogDto
    {
        public long Id { get; set; }
        public string EntityType { get; set; } = default!;
        public string EntityId { get; set; } = default!;
        public string Action { get; set; } = default!;
        public string? Summary { get; set; }
        public string? DetailsJson { get; set; }
        public Guid? UserId { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime OccurredAtUtc { get; set; }
    }
}
