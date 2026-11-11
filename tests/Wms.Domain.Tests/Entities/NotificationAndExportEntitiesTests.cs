using FluentAssertions;
using Wms.Domain.Entities.Exports;
using Wms.Domain.Entities.Notifications;
using Wms.Domain.Enums;
using Xunit;

namespace Wms.Domain.Tests.Entities;

public class NotificationAndExportEntitiesTests
{
    [Fact]
    public void AppNotification_DefaultState_ShouldBeUnread()
    {
        var notification = new AppNotification
        {
            Title = "Stok Menipis",
            Message = "SKU-001 mencapai batas minimum",
            Category = NotificationCategory.StockAlert,
            DedupKey = "LowStock:WH-01:SKU-001"
        };

        notification.Status.Should().Be(NotificationStatus.Unread);
        notification.ReadAtUtc.Should().BeNull();
        notification.ResolvedAtUtc.Should().BeNull();

        var now = DateTime.UtcNow;
        notification.MarkAsRead(now);
        notification.Status.Should().Be(NotificationStatus.Read);
        notification.ReadAtUtc.Should().Be(now);

        notification.Resolve(now);
        notification.Status.Should().Be(NotificationStatus.Resolved);
        notification.ResolvedAtUtc.Should().Be(now);
    }

    [Fact]
    public void ExportJob_Lifecycle_ShouldTransitionCorrectly()
    {
        var job = new ExportJob
        {
            RequestedByUserId = "usr-1",
            RequestedByUserName = "Admin",
            ReportType = "StockReport",
            Format = ExportFormat.Csv
        };

        job.Status.Should().Be(ExportJobStatus.Pending);

        job.MarkProcessing();
        job.Status.Should().Be(ExportJobStatus.Processing);

        var now = DateTime.UtcNow;
        job.MarkCompleted("App_Data/Exports/test.csv", "StockReport-20261111.csv", 100, 2048, now);
        job.Status.Should().Be(ExportJobStatus.Completed);
        job.RowCount.Should().Be(100);
        job.FileSizeBytes.Should().Be(2048);
        job.CompletedAtUtc.Should().Be(now);

        job.MarkExpired();
        job.Status.Should().Be(ExportJobStatus.Expired);
    }
}
