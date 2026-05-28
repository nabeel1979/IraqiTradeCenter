using System;

namespace IraqiTradeCenterCompany.API.Auth.Notifications;

/// <summary>
/// إشعار داخلي يُرسَل لمستخدم محدَّد بحدث معيّن (مناقلة، etc.).
/// يُخزَّن في جدول auth.Notifications.
/// </summary>
public class Notification
{
    public int    Id         { get; private set; }
    /// <summary>معرّف المستخدم المستهدَف (GUID كنص)</summary>
    public string UserId     { get; private set; } = default!;
    public string Title      { get; private set; } = default!;
    public string Body       { get; private set; } = default!;
    /// <summary>مسار الواجهة الأمامية المرتبط بالإشعار (مثلاً: /accounting/cash-boxes?tab=transfers&id=5)</summary>
    public string? Link      { get; private set; }
    public bool   IsRead     { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? EntityType { get; private set; }
    public string? EntityId   { get; private set; }

    private Notification() { }

    public static Notification Create(
        string userId,
        string title,
        string body,
        string? link        = null,
        string? entityType  = null,
        string? entityId    = null)
    {
        return new Notification
        {
            UserId     = userId,
            Title      = title,
            Body       = body,
            Link       = link,
            IsRead     = false,
            CreatedAt  = DateTime.UtcNow,
            EntityType = entityType,
            EntityId   = entityId,
        };
    }

    public void MarkRead() => IsRead = true;
}
