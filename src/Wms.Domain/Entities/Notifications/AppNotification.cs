using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Notifications;

public class AppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserId { get; set; }
    public string? TargetRole { get; set; }
    public NotificationCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public string DedupKey { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; } = NotificationStatus.Unread;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public void MarkAsRead(DateTime readAtUtc)
    {
        if (Status == NotificationStatus.Unread)
        {
            Status = NotificationStatus.Read;
            ReadAtUtc = readAtUtc;
        }
    }

    public void Resolve(DateTime resolvedAtUtc)
    {
        Status = NotificationStatus.Resolved;
        ResolvedAtUtc = resolvedAtUtc;
    }
}

