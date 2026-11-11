using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Notifications;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Notifications;
using Xunit;

namespace Wms.Integration.Tests.Notifications;

public class NotificationServiceTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    [Fact]
    public async Task PublishNotification_ShouldCreateNewNotification_WhenDedupKeyDoesNotExist()
    {
        using var context = CreateDbContext();
        var service = new NotificationService(context);

        var request = new PublishNotificationRequest(
            UserId: "user-1",
            TargetRole: null,
            Category: NotificationCategory.StockAlert,
            Title: "Stok Menipis",
            Message: "Barang A tersisa 2 unit",
            LinkUrl: "/reports/stock",
            DedupKey: "LowStock:WH-01:ITEM-A"
        );

        var result = await service.PublishNotificationAsync(request);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeEmpty();

        var notification = await context.AppNotifications.FindAsync(result.Data);
        notification.Should().NotBeNull();
        notification!.Status.Should().Be(NotificationStatus.Unread);
        notification.DedupKey.Should().Be("LowStock:WH-01:ITEM-A");
    }

    [Fact]
    public async Task PublishNotification_ShouldDeduplicate_WhenActiveNotificationExists()
    {
        using var context = CreateDbContext();
        var service = new NotificationService(context);

        var request1 = new PublishNotificationRequest(
            UserId: "user-1",
            TargetRole: null,
            Category: NotificationCategory.StockAlert,
            Title: "Stok Menipis",
            Message: "Barang A tersisa 2 unit",
            LinkUrl: "/reports/stock",
            DedupKey: "LowStock:WH-01:ITEM-A"
        );

        var result1 = await service.PublishNotificationAsync(request1);
        result1.Succeeded.Should().BeTrue();

        // Second call with same DedupKey
        var request2 = new PublishNotificationRequest(
            UserId: "user-1",
            TargetRole: null,
            Category: NotificationCategory.StockAlert,
            Title: "Stok Menipis Sekali",
            Message: "Barang A tersisa 1 unit",
            LinkUrl: "/reports/stock",
            DedupKey: "LowStock:WH-01:ITEM-A"
        );

        var result2 = await service.PublishNotificationAsync(request2);
        result2.Succeeded.Should().BeTrue();
        result2.Data.Should().Be(result1.Data); // Returned same notification ID

        var total = await context.AppNotifications.CountAsync(n => n.DedupKey == "LowStock:WH-01:ITEM-A");
        total.Should().Be(1); // No duplicate created
    }

    [Fact]
    public async Task ResolveNotification_ShouldMarkActiveAsResolved_AndAllowNewTriggerAfterRecovery()
    {
        using var context = CreateDbContext();
        var service = new NotificationService(context);

        var request = new PublishNotificationRequest(
            UserId: null,
            TargetRole: "SuperAdmin",
            Category: NotificationCategory.Reconciliation,
            Title: "Selisih Ledger",
            Message: "Ditemukan selisih saldo di gudang",
            LinkUrl: "/admin/audit-logs",
            DedupKey: "ReconDiff:WH-01:ITEM-A"
        );

        var result1 = await service.PublishNotificationAsync(request);
        result1.Succeeded.Should().BeTrue();

        // Resolve condition (kondisi pulih)
        var resolveResult = await service.ResolveNotificationByDedupKeyAsync("ReconDiff:WH-01:ITEM-A");
        resolveResult.Succeeded.Should().BeTrue();

        var notification1 = await context.AppNotifications.FindAsync(result1.Data);
        notification1!.Status.Should().Be(NotificationStatus.Resolved);
        notification1.ResolvedAtUtc.Should().NotBeNull();

        // After recovery, when condition happens again, a new notification is generated
        var result2 = await service.PublishNotificationAsync(request);
        result2.Succeeded.Should().BeTrue();
        result2.Data.Should().NotBe(result1.Data); // New notification ID

        var total = await context.AppNotifications.CountAsync(n => n.DedupKey == "ReconDiff:WH-01:ITEM-A");
        total.Should().Be(2);

        var activeCount = await context.AppNotifications.CountAsync(n => n.DedupKey == "ReconDiff:WH-01:ITEM-A" && n.Status != NotificationStatus.Resolved);
        activeCount.Should().Be(1);
    }

    [Fact]
    public async Task MarkAsReadAndSummary_ShouldUpdateStatusAndCountCorrectly()
    {
        using var context = CreateDbContext();
        var service = new NotificationService(context);

        var id1 = (await service.PublishNotificationAsync(new PublishNotificationRequest(
            "user-1", null, NotificationCategory.StockAlert, "Alert 1", "Msg 1", null, "K1"))).Data;

        var id2 = (await service.PublishNotificationAsync(new PublishNotificationRequest(
            "user-1", null, NotificationCategory.PurchaseOrder, "Alert 2", "Msg 2", null, "K2"))).Data;

        var summaryBefore = await service.GetUnreadSummaryAsync("user-1");
        summaryBefore.TotalUnread.Should().Be(2);

        // Mark 1 as read
        await service.MarkAsReadAsync(id1, "user-1");

        var summaryAfter = await service.GetUnreadSummaryAsync("user-1");
        summaryAfter.TotalUnread.Should().Be(1);
        summaryAfter.RecentNotifications.First().Id.Should().Be(id2);

        // Mark all as read
        await service.MarkAllAsReadAsync("user-1");

        var summaryFinal = await service.GetUnreadSummaryAsync("user-1");
        summaryFinal.TotalUnread.Should().Be(0);
    }
}
