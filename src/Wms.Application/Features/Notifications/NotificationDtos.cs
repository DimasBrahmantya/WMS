using Wms.Domain.Enums;

namespace Wms.Application.Features.Notifications;

public record PublishNotificationRequest(
    string? UserId,
    string? TargetRole,
    NotificationCategory Category,
    string Title,
    string Message,
    string? LinkUrl,
    string DedupKey
);

public record NotificationFilterRequest(
    NotificationStatus? Status = null,
    NotificationCategory? Category = null,
    string? SearchTerm = null,
    int PageNumber = 1,
    int PageSize = 20
);

public class NotificationDto
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public string? TargetRole { get; set; }
    public NotificationCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public string DedupKey { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

public class UnreadNotificationSummaryDto
{
    public int TotalUnread { get; set; }
    public List<NotificationDto> RecentNotifications { get; set; } = new();
}
