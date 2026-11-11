using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Exports;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Audit;
using Wms.Infrastructure.Services.Exports;
using Wms.Infrastructure.Services.Exports.FileStorage;
using Wms.Infrastructure.Services.Exports.Formatters;
using Wms.Infrastructure.Services.Notifications;
using Xunit;

namespace Wms.Integration.Tests.Exports;

public class ExportIntegrationTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    [Fact]
    public async Task ExportJob_EndToEndLifecycle_ShouldProcessQueueNotifyAndDownload()
    {
        using var context = CreateDbContext();
        var tempStoragePath = Path.Combine(Path.GetTempPath(), "wms_export_tests_" + Guid.NewGuid().ToString("N"));
        var fileStorage = new LocalExportFileStorage();

        var formatters = new IReportFormatter[]
        {
            new CsvReportFormatter(),
            new ExcelReportFormatter(),
            new PdfReportFormatter()
        };
        var auditService = new AppAuditService(context);
        var notifService = new NotificationService(context);
        var exportService = new ExportService(context, fileStorage, formatters, auditService, notifService);

        // Seed some stock data
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Code = "WH-EXP", Name = "Gudang Ekspor" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Elektronik" };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Unit", Abbreviation = "UNT" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-EXP-1",
            Name = "Monitor 24 Inch",
            CategoryId = category.Id,
            UnitId = unit.Id,
            IsActive = true
        };
        var location = new WarehouseLocation { Id = Guid.NewGuid(), WarehouseId = warehouse.Id, Code = "LOC-E1", Name = "Rak E1" };
        var balance = new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            OnHand = 25
        };

        context.Warehouses.Add(warehouse);
        context.Categories.Add(category);
        context.Units.Add(unit);
        context.Products.Add(product);
        context.WarehouseLocations.Add(location);
        context.InventoryBalances.Add(balance);
        await context.SaveChangesAsync();

        var userId = Guid.NewGuid().ToString();

        // 1. Queue background export job
        var queueReq = new QueueExportJobRequest(
            ReportType: "StockReport",
            Format: ExportFormat.Xlsx,
            FilterPayloadJson: "{}",
            RequestedByUserId: userId,
            RequestedByUserName: "StaffGudang"
        );

        var queueResult = await exportService.QueueExportJobAsync(queueReq);
        queueResult.Succeeded.Should().BeTrue();
        var jobId = queueResult.Data;

        var queuedJob = await exportService.GetExportJobAsync(jobId);
        queuedJob.Should().NotBeNull();
        queuedJob!.Status.Should().Be(ExportJobStatus.Pending);

        // 2. Process export job (simulates BackgroundExportWorkerJob)
        var processResult = await exportService.ProcessExportJobAsync(jobId);
        processResult.Succeeded.Should().BeTrue();

        var completedJob = await exportService.GetExportJobAsync(jobId);
        completedJob!.Status.Should().Be(ExportJobStatus.Completed);
        completedJob.RowCount.Should().Be(1);
        completedJob.FileSizeBytes.Should().BeGreaterThan(0);

        // 3. Verify in-app notification was sent to user
        var notifs = await notifService.GetUserNotificationsAsync(new(), userId);
        notifs.Items.Should().ContainSingle(n => n.Category == NotificationCategory.SystemExport && n.DedupKey == $"ExportJob:{jobId}");

        // 4. Download file as requesting user -> MUST SUCCEED
        var downloadResult = await exportService.GetExportFileAsync(jobId, userId, isSuperAdmin: false);
        downloadResult.Succeeded.Should().BeTrue();
        downloadResult.Data.FileBytes.Should().NotBeEmpty();
        downloadResult.Data.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        downloadResult.Data.FileName.Should().EndWith(".xlsx");

        // 5. Unauthorized user -> MUST BE REJECTED
        var unauthorizedResult = await exportService.GetExportFileAsync(jobId, "intruder-user-id", isSuperAdmin: false);
        unauthorizedResult.Succeeded.Should().BeFalse();
        unauthorizedResult.ErrorMessage.Should().Contain("tidak memiliki hak akses");

        // 6. SuperAdmin override -> MUST SUCCEED
        var adminResult = await exportService.GetExportFileAsync(jobId, "superadmin-id", isSuperAdmin: true);
        adminResult.Succeeded.Should().BeTrue();

        // 7. Verify audit logs recorded both Queue, Direct/Processing, and Download
        var auditLogs = await context.AuditLogs.Where(a => a.EntityType == "ExportJob").ToListAsync();
        auditLogs.Should().Contain(a => a.Action == "QueueExport");
        auditLogs.Should().Contain(a => a.Action == "Download");
    }
}

