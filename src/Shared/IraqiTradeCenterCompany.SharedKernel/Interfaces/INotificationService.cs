using System.Threading;
using System.Threading.Tasks;

namespace IraqiTradeCenterCompany.SharedKernel.Interfaces;

public interface INotificationService
{
    Task NotifyUserAsync(
        string userId,
        string title,
        string body,
        string? link       = null,
        string? entityType = null,
        string? entityId   = null,
        CancellationToken ct = default);

    /// <summary>
    /// يُرسل إشعاراً لجميع المستخدمين المرتبطين بصندوق معيّن
    /// باستثناء المستخدم الحالي (المُرسِل).
    /// </summary>
    Task NotifyCashBoxUsersAsync(
        int    cashBoxId,
        string excludeUserId,
        string title,
        string body,
        string? link       = null,
        string? entityType = null,
        string? entityId   = null,
        CancellationToken ct = default);
}
