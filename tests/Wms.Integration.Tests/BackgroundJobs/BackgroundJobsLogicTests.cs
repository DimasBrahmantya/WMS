using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Wms.Application.Features.Audit;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;
using Wms.Infrastructure.BackgroundJobs;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Audit;
using Wms.Infrastructure.Services.Notifications;
using Wms.Infrastructure.Services.Stock;
using Xunit;

namespace Wms.Integration.Tests.BackgroundJobs;

public class BackgroundJobsLogicTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    private class TestJobExecutionContext : IJobExecutionContext
    {
        public CancellationToken CancellationToken => CancellationToken.None;
        public IJobDetail JobDetail => null!;
        public ITrigger Trigger => null!;
        public ICalendar Calendar => null!;
        public bool Recovering => false;
        public TriggerKey TriggerKey => null!;
        public string FireInstanceId => string.Empty;
        public DateTimeOffset FireTimeUtc => DateTimeOffset.UtcNow;
        public DateTimeOffset? ScheduledFireTimeUtc => null;
        public DateTimeOffset? PrevFireTimeUtc => null;
        public DateTimeOffset? PreviousFireTimeUtc => null;
        public DateTimeOffset? NextFireTimeUtc => null;
        public TriggerKey RecoveringTriggerKey => null!;
        public int RefireCount => 0;
        public IJob JobInstance => null!;
        public JobDataMap MergedJobDataMap => new();
        public object? Result { get; set; }
        public TimeSpan JobRunTime => TimeSpan.Zero;
        public void Put(object key, object objectValue) { }
        public object Get(object key) => null!;
        public IScheduler Scheduler => null!;
    }

    [Fact]
    public async Task StockAlertJob_ShouldAlertLowStock_AndResolveWhenReplenished()
    {
        using var context = CreateDbContext();
        var notifService = new NotificationService(context);
        var job = new StockAlertJob(context, notifService);

        var warehouse = new Warehouse { Id = Guid.NewGuid(), Code = "WH-01", Name = "Gudang Utama" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Umum" };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Pieces", Abbreviation = "PCS" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-ALERT-1",
            Name = "Barang Kritis",
            CategoryId = category.Id,
            UnitId = unit.Id,
            MinStock = 10,
            IsActive = true
        };
        var location = new WarehouseLocation { Id = Guid.NewGuid(), WarehouseId = warehouse.Id, Code = "LOC-1", Name = "Rak 1" };

        var balance = new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            OnHand = 5 // <= MinimumStockLevel 10
        };

        context.Warehouses.Add(warehouse);
        context.Categories.Add(category);
        context.Units.Add(unit);
        context.Products.Add(product);
        context.WarehouseLocations.Add(location);
        context.InventoryBalances.Add(balance);
        await context.SaveChangesAsync();

        var executionContext = new TestJobExecutionContext();

        // 1. Run job -> should trigger low stock alert
        await job.Execute(executionContext);

        var notifs = await context.AppNotifications.ToListAsync();
        notifs.Should().ContainSingle(n => n.Category == NotificationCategory.StockAlert && n.Status == NotificationStatus.Unread);
        notifs.First().DedupKey.Should().Be($"LowStock:{warehouse.Id}:{product.Id}");

        // 2. Replenish stock to 20 (> 10)
        balance.OnHand = 20;
        await context.SaveChangesAsync();

        // Run job again -> should resolve notification
        await job.Execute(executionContext);

        var resolvedNotif = await context.AppNotifications.FirstAsync();
        resolvedNotif.Status.Should().Be(NotificationStatus.Resolved);
        resolvedNotif.ResolvedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task OverduePurchaseOrderJob_ShouldTriggerAlertForPastDuePOs()
    {
        using var context = CreateDbContext();
        var notifService = new NotificationService(context);
        var job = new OverduePurchaseOrderJob(context, notifService);

        var warehouse = new Warehouse { Id = Guid.NewGuid(), Code = "WH-PO", Name = "Gudang PO" };
        var supplier = new Supplier { Id = Guid.NewGuid(), Code = "SUP-1", Name = "PT Pemasok" };
        var creatorId = Guid.NewGuid();
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-202611-001",
            WarehouseId = warehouse.Id,
            SupplierId = supplier.Id,
            OrderDate = DateTime.UtcNow.AddDays(-10),
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-2), // past due!
            Status = PurchaseOrderStatus.Ordered,
            CreatedByUserId = creatorId
        };

        context.Warehouses.Add(warehouse);
        context.Suppliers.Add(supplier);
        context.PurchaseOrders.Add(po);
        await context.SaveChangesAsync();

        await job.Execute(new TestJobExecutionContext());

        var notifs = await context.AppNotifications.ToListAsync();
        notifs.Should().ContainSingle(n => n.Category == NotificationCategory.PurchaseOrder && n.DedupKey == $"OverduePo:{po.Id}");
    }

    [Fact]
    public async Task ReconciliationJob_ShouldDetectDiscrepancyAndLogAudit()
    {
        using var context = CreateDbContext();
        var notifService = new NotificationService(context);
        var auditService = new AppAuditService(context);
        var reconService = new StockReconciliationService(context);
        var job = new ReconciliationJob(reconService, notifService, auditService);

        var warehouse = new Warehouse { Id = Guid.NewGuid(), Code = "WH-REC", Name = "Gudang Recon" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Umum" };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Pieces", Abbreviation = "PCS" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-REC-1",
            Name = "Barang Selisih",
            CategoryId = category.Id,
            UnitId = unit.Id,
            IsActive = true
        };
        var location = new WarehouseLocation { Id = Guid.NewGuid(), WarehouseId = warehouse.Id, Code = "LOC-R", Name = "Rak R" };

        // Balance says 50, but zero movements in ledger -> discrepancy!
        var balance = new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            OnHand = 50
        };

        context.Warehouses.Add(warehouse);
        context.Categories.Add(category);
        context.Units.Add(unit);
        context.Products.Add(product);
        context.WarehouseLocations.Add(location);
        context.InventoryBalances.Add(balance);
        await context.SaveChangesAsync();

        await job.Execute(new TestJobExecutionContext());

        // Verify notification
        var notifs = await context.AppNotifications.ToListAsync();
        notifs.Should().ContainSingle(n => n.Category == NotificationCategory.Reconciliation);

        // Verify audit log
        var audits = await context.AuditLogs.ToListAsync();
        audits.Should().ContainSingle(a => a.Action == "DiscrepancyDetected" && a.EntityType == "Reconciliation");
    }
}
