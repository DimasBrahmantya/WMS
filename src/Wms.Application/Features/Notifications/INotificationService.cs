using Wms.Application.Common.Models;

namespace Wms.Application.Features.Notifications;

public interface INotificationService
{
    Task<Result<Guid>> PublishNotificationAsync(PublishNotificationRequest request, CancellationToken cancellationToken = default);
    Task<Result> ResolveNotificationByDedupKeyAsync(string dedupKey, CancellationToken cancellationToken = default);
    Task<Result> MarkAsReadAsync(Guid notificationId, string userId, CancellationToken cancellationToken = default);
    Task<Result> MarkAllAsReadAsync(string userId, string? userRole = null, CancellationToken cancellationToken = default);
    Task<PaginatedList<NotificationDto>> GetUserNotificationsAsync(NotificationFilterRequest filter, string userId, string? userRole = null, CancellationToken cancellationToken = default);
    Task<UnreadNotificationSummaryDto> GetUnreadSummaryAsync(string userId, string? userRole = null, int recentCount = 5, CancellationToken cancellationToken = default);
}
