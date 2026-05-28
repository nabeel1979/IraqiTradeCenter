using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Auth.Notifications;

public class NotificationService : INotificationService
{
    private readonly AuthDbContext _db;

    public NotificationService(AuthDbContext db) => _db = db;

    public async Task NotifyUserAsync(
        string userId,
        string title,
        string body,
        string? link       = null,
        string? entityType = null,
        string? entityId   = null,
        CancellationToken ct = default)
    {
        try
        {
            var n = Notification.Create(userId, title, body, link, entityType, entityId);
            _db.Notifications.Add(n);
            await _db.SaveChangesAsync(ct);
        }
        catch { /* لا نُفشل العملية الأصلية */ }
    }

    public async Task NotifyCashBoxUsersAsync(
        int    cashBoxId,
        string excludeUserId,
        string title,
        string body,
        string? link       = null,
        string? entityType = null,
        string? entityId   = null,
        CancellationToken ct = default)
    {
        try
        {
            Guid.TryParse(excludeUserId, out var excludeGuid);
            var userIds = await _db.UserCashBoxes
                .Where(x => x.CashBoxId == cashBoxId && x.UserId != excludeGuid)
                .Select(x => x.UserId.ToString())
                .Distinct()
                .ToListAsync(ct);

            // إذا لم يكن هناك مستخدمون محدَّدون → لا نرسل
            if (userIds.Count == 0) return;

            foreach (var uid in userIds)
            {
                var n = Notification.Create(uid, title, body, link, entityType, entityId);
                _db.Notifications.Add(n);
            }
            await _db.SaveChangesAsync(ct);
        }
        catch { /* لا نُفشل العملية الأصلية */ }
    }
}
