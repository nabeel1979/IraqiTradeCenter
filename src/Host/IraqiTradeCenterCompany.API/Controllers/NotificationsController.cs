using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.API.Auth.Notifications;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Controllers;

/// <summary>
/// إشعارات المستخدم الحالي.
///   GET    /api/notifications          — القائمة (آخر 50، غير المقروءة أولاً)
///   GET    /api/notifications/unread-count — عدد الغير مقروءة فقط (للبِل في الـ TopBar)
///   POST   /api/notifications/{id}/read   — تعليم إشعار واحد كمقروء
///   POST   /api/notifications/read-all    — تعليم الكل كمقروء
/// </summary>
[Route("api/notifications")]
public class NotificationsController : BaseApiController
{
    private readonly AuthDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(AuthDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var uid = _currentUser.UserId?.ToString();
        if (string.IsNullOrEmpty(uid)) return Forbid();

        var rows = await _db.Notifications
            .Where(n => n.UserId == uid)
            .OrderBy(n => n.IsRead)
            .ThenByDescending(n => n.CreatedAt)
            .Take(80)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Body,
                n.Link,
                n.IsRead,
                n.EntityType,
                n.EntityId,
                n.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = rows });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var uid = _currentUser.UserId?.ToString();
        if (string.IsNullOrEmpty(uid)) return Ok(new { success = true, count = 0 });

        var count = await _db.Notifications
            .CountAsync(n => n.UserId == uid && !n.IsRead, ct);

        return Ok(new { success = true, count });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        var uid = _currentUser.UserId?.ToString();
        if (string.IsNullOrEmpty(uid)) return Forbid();

        var n = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct);
        if (n == null) return NotFound();

        n.MarkRead();
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var uid = _currentUser.UserId?.ToString();
        if (string.IsNullOrEmpty(uid)) return Forbid();

        var unread = await _db.Notifications
            .Where(n => n.UserId == uid && !n.IsRead)
            .ToListAsync(ct);

        foreach (var n in unread) n.MarkRead();
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, marked = unread.Count });
    }
}
