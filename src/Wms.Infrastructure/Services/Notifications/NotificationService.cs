using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Notifications;
using Wms.Domain.Entities.Notifications;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly IWmsDbContext _context;

    public NotificationService(IWmsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> PublishNotificationAsync(
        PublishNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DedupKey))
        {
            return Result<Guid>.Failure("DedupKey wajib diisi.");
        }

        // Deduplikasi: 1 notifikasi aktif per kondisi per entitas
        var existingActive = await _context.AppNotifications
            .FirstOrDefaultAsync(n => n.DedupKey == request.DedupKey && n.Status != NotificationStatus.Resolved, cancellationToken);

        if (existingActive != null)
        {
            return Result<Guid>.Success(existingActive.Id);
        }

        var notification = new AppNotification
        {
            UserId = request.UserId,
            TargetRole = request.TargetRole,
            Category = request.Category,
            Title = request.Title,
            Message = request.Message,
            LinkUrl = request.LinkUrl,
            DedupKey = request.DedupKey,
            Status = NotificationStatus.Unread,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.AppNotifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(notification.Id);
    }

    public async Task<Result> ResolveNotificationByDedupKeyAsync(
        string dedupKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dedupKey))
        {
            return Result.Failure("DedupKey wajib diisi.");
        }

        var activeNotifications = await _context.AppNotifications
            .Where(n => n.DedupKey == dedupKey && n.Status != NotificationStatus.Resolved)
            .ToListAsync(cancellationToken);

        if (activeNotifications.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var n in activeNotifications)
            {
                n.Resolve(now);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> MarkAsReadAsync(
        Guid notificationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _context.AppNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

        if (notification == null)
        {
            return Result.Failure("Notifikasi tidak ditemukan.");
        }

        notification.MarkAsRead(DateTime.UtcNow);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> MarkAllAsReadAsync(
        string userId,
        string? userRole = null,
        CancellationToken cancellationToken = default)
    {
        var query = GetUserVisibleQuery(userId, userRole)
            .Where(n => n.Status == NotificationStatus.Unread);

        var unreadNotifications = await query.ToListAsync(cancellationToken);
        if (unreadNotifications.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var n in unreadNotifications)
            {
                n.MarkAsRead(now);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<PaginatedList<NotificationDto>> GetUserNotificationsAsync(
        NotificationFilterRequest filter,
        string userId,
        string? userRole = null,
        CancellationToken cancellationToken = default)
    {
        var query = GetUserVisibleQuery(userId, userRole);

        if (filter.Status.HasValue)
        {
            query = query.Where(n => n.Status == filter.Status.Value);
        }

        if (filter.Category.HasValue)
        {
            query = query.Where(n => n.Category == filter.Category.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(n => n.Title.ToLower().Contains(term) || n.Message.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                TargetRole = n.TargetRole,
                Category = n.Category,
                Title = n.Title,
                Message = n.Message,
                LinkUrl = n.LinkUrl,
                DedupKey = n.DedupKey,
                Status = n.Status,
                CreatedAtUtc = n.CreatedAtUtc,
                ReadAtUtc = n.ReadAtUtc,
                ResolvedAtUtc = n.ResolvedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<NotificationDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<UnreadNotificationSummaryDto> GetUnreadSummaryAsync(
        string userId,
        string? userRole = null,
        int recentCount = 5,
        CancellationToken cancellationToken = default)
    {
        var query = GetUserVisibleQuery(userId, userRole)
            .Where(n => n.Status == NotificationStatus.Unread);

        var totalUnread = await query.CountAsync(cancellationToken);

        var recent = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(recentCount)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                TargetRole = n.TargetRole,
                Category = n.Category,
                Title = n.Title,
                Message = n.Message,
                LinkUrl = n.LinkUrl,
                DedupKey = n.DedupKey,
                Status = n.Status,
                CreatedAtUtc = n.CreatedAtUtc,
                ReadAtUtc = n.ReadAtUtc,
                ResolvedAtUtc = n.ResolvedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new UnreadNotificationSummaryDto
        {
            TotalUnread = totalUnread,
            RecentNotifications = recent
        };
    }

    private IQueryable<AppNotification> GetUserVisibleQuery(string userId, string? userRole)
    {
        return _context.AppNotifications.Where(n =>
            n.UserId == userId ||
            (n.UserId == null && (n.TargetRole == null || n.TargetRole == userRole || userRole == "SuperAdmin")));
    }
}
