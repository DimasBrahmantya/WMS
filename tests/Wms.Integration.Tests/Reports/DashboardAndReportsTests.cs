using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Models;
using Wms.Application.Features.Dashboard;
using Wms.Application.Features.Reports;
using Wms.Application.Features.Audit;
using Wms.Domain.Common;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Entities.System;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Audit;
using Wms.Infrastructure.Services.Dashboard;
using Wms.Infrastructure.Services.Reports;
using Xunit;

namespace Wms.Integration.Tests.Reports;

public class TestUserContext : IUserContext
{
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public string? Username { get; set; } = "testuser";
    public string? FullName { get; set; } = "Test User";
    public IReadOnlyList<string> Roles { get; set; } = new[] { "SuperAdmin" };
    public IReadOnlyList<string> Permissions { get; set; } = Domain.Authorization.Permissions.All;
    public IReadOnlyList<Guid> AllowedWarehouseIds { get; set; } = Array.Empty<Guid>();
    public bool IsAuthenticated => true;
    public bool HasPermission(string permission) => true;
    public bool CanAccessWarehouse(Guid warehouseId) =>
        AllowedWarehouseIds.Count == 0 || AllowedWarehouseIds.Contains(warehouseId);
}

public class TestClock : IClock
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    public DateTime TodayJakarta => JakartaTime.Today;
}

public class DashboardAndReportsTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    private (Warehouse wh1, Warehouse wh2, Product prodA, Product prodB, ApplicationUser user) SeedBaseData(WmsDbContext context)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "admin",
            FullName = "Admin Gudang",
            Email = "admin@wms.local"
        };
        context.Users.Add(user);

        var wh1 = new Warehouse { Id = Guid.NewGuid(), Code = "WH-JKT", Name = "Gudang Jakarta", IsActive = true };
        var wh2 = new Warehouse { Id = Guid.NewGuid(), Code = "WH-SBY", Name = "Gudang Surabaya", IsActive = true };
        context.Warehouses.AddRange(wh1, wh2);

        var loc1 = new WarehouseLocation { Id = Guid.NewGuid(), WarehouseId = wh1.Id, Code = "JKT-R1", Name = "Rak 1", IsActive = true };
        var loc2 = new WarehouseLocation { Id = Guid.NewGuid(), WarehouseId = wh2.Id, Code = "SBY-R1", Name = "Rak 1", IsActive = true };
        context.WarehouseLocations.AddRange(loc1, loc2);

        var catElektronik = new Category { Id = Guid.NewGuid(), Name = "Elektronik", IsActive = true };
        var catPangan = new Category { Id = Guid.NewGuid(), Name = "Pangan", IsActive = true };
        context.Categories.AddRange(catElektronik, catPangan);

        var unitPcs = new Unit { Id = Guid.NewGuid(), Name = "Pcs", Abbreviation = "PCS", IsActive = true };
        var unitBox = new Unit { Id = Guid.NewGuid(), Name = "Box", Abbreviation = "BOX", IsActive = true };
        context.Units.AddRange(unitPcs, unitBox);

        var prodA = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-LAPTOP",
            Barcode = "8990001",
            Name = "Laptop Pro",
            CategoryId = catElektronik.Id,
            UnitId = unitPcs.Id,
            MinStock = 5m,
            IsActive = true
        };

        var prodB = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-SNACK",
            Barcode = "8990002",
            Name = "Biskuit Gandum",
            CategoryId = catPangan.Id,
            UnitId = unitBox.Id,
            MinStock = 20m,
            IsActive = true
        };
        context.Products.AddRange(prodA, prodB);

        // Saldo prodA di wh1: OnHand = 15, Reserved = 2
        context.InventoryBalances.Add(new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = prodA.Id,
            WarehouseId = wh1.Id,
            WarehouseLocationId = loc1.Id,
            OnHand = 15m,
            Reserved = 2m,
            Status = StockStatus.Available
        });

        // Saldo prodB di wh1: OnHand = 10, Reserved = 0 (Low stock karena MinStock = 20)
        context.InventoryBalances.Add(new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = prodB.Id,
            WarehouseId = wh1.Id,
            WarehouseLocationId = loc1.Id,
            OnHand = 10m,
            Reserved = 0m,
            Status = StockStatus.Available
        });

        // Valuasi prodA di wh1: OnHandQty = 15, AvgCost = 10.000.000, Total = 150.000.000
        context.InventoryValuations.Add(new InventoryValuation
        {
            Id = Guid.NewGuid(),
            ProductId = prodA.Id,
            WarehouseId = wh1.Id,
            OnHandQty = 15m,
            AvgCost = 10_000_000m,
            OnHandValue = 150_000_000m,
            LastCost = 10_000_000m
        });

        // Valuasi prodB di wh1: OnHandQty = 10, AvgCost = 50.000, Total = 500.000
        context.InventoryValuations.Add(new InventoryValuation
        {
            Id = Guid.NewGuid(),
            ProductId = prodB.Id,
            WarehouseId = wh1.Id,
            OnHandQty = 10m,
            AvgCost = 50_000m,
            OnHandValue = 500_000m,
            LastCost = 50_000m
        });

        context.SaveChanges();
        return (wh1, wh2, prodA, prodB, user);
    }

    [Fact]
    public async Task DashboardService_ShouldReturnAccurateAggregations()
    {
        using var context = CreateDbContext();
        var (wh1, wh2, prodA, prodB, user) = SeedBaseData(context);

        var clock = new TestClock();
        var userContext = new TestUserContext();
        var service = new DashboardService(context, userContext, clock);

        var summary = await service.GetDashboardSummaryAsync(new DashboardFilterDto());

        summary.TotalActiveSku.Should().Be(2);
        summary.TotalActiveWarehouses.Should().Be(2);
        summary.TotalValuation.Should().Be(150_500_000m); // 150jt + 500rb
        summary.QuantitiesByUnit.Should().Contain(u => u.UnitName == "Pcs" && u.TotalQuantity == 15m);
        summary.QuantitiesByUnit.Should().Contain(u => u.UnitName == "Box" && u.TotalQuantity == 10m);
        summary.LowStockCount.Should().Be(1); // ProdB has 10 on hand, min is 20
        summary.OutOfStockCount.Should().Be(0);
        summary.ValuationByWarehouse.Should().Contain(w => w.WarehouseId == wh1.Id && w.TotalValue == 150_500_000m);
    }

    [Fact]
    public async Task DashboardService_WithWarehouseFilter_ShouldRestrictData()
    {
        using var context = CreateDbContext();
        var (wh1, wh2, prodA, prodB, user) = SeedBaseData(context);

        var clock = new TestClock();
        var userContext = new TestUserContext();
        var service = new DashboardService(context, userContext, clock);

        // Filter to Surabaya (empty warehouse)
        var summary = await service.GetDashboardSummaryAsync(new DashboardFilterDto { WarehouseId = wh2.Id });

        summary.TotalValuation.Should().Be(0m);
        summary.QuantitiesByUnit.Should().BeEmpty();
    }

    [Fact]
    public async Task CurrentStockReport_ShouldFilterAndCalculateAvailable()
    {
        using var context = CreateDbContext();
        var (wh1, wh2, prodA, prodB, user) = SeedBaseData(context);

        var clock = new TestClock();
        var userContext = new TestUserContext();
        var service = new ReportService(context, userContext, clock);

        var report = await service.GetCurrentStockReportAsync(new StockReportQuery { WarehouseId = wh1.Id });

        report.TotalCount.Should().Be(2);
        var itemA = report.Items.First(i => i.ProductId == prodA.Id);
        itemA.OnHand.Should().Be(15m);
        itemA.Reserved.Should().Be(2m);
        itemA.Available.Should().Be(13m); // OnHand - Reserved
        itemA.Status.Should().Be("Aman");

        var itemB = report.Items.First(i => i.ProductId == prodB.Id);
        itemB.OnHand.Should().Be(10m);
        itemB.MinStock.Should().Be(20m);
        itemB.Status.Should().Be("Rendah");
    }

    [Fact]
    public async Task CurrentStockReport_FilterByStatus_ShouldReturnLowStockOnly()
    {
        using var context = CreateDbContext();
        var (wh1, wh2, prodA, prodB, user) = SeedBaseData(context);

        var clock = new TestClock();
        var userContext = new TestUserContext();
        var service = new ReportService(context, userContext, clock);

        var report = await service.GetCurrentStockReportAsync(new StockReportQuery { StockStatus = "low" });

        report.TotalCount.Should().Be(1);
        report.Items[0].ProductId.Should().Be(prodB.Id);
        report.Items[0].Status.Should().Be("Rendah");
    }

    [Fact]
    public async Task StockCardReport_ShouldCalculateOpeningBalanceAndRunningBalanceCorrectly()
    {
        using var context = CreateDbContext();
        var (wh1, wh2, prodA, prodB, user) = SeedBaseData(context);
        var loc1 = await context.WarehouseLocations.FirstAsync(l => l.WarehouseId == wh1.Id);

        var todayJakarta = JakartaTime.Today;
        var startOfMonth = new DateTime(todayJakarta.Year, todayJakarta.Month, 1);
        var priorDateUtc = JakartaTime.ToUtcStartOfDay(startOfMonth.AddDays(-5)); // 5 days before start date

        // 1. Mutasi sebelum periode filter (Membentuk Saldo Awal): Masuk 20, Keluar 8 => Saldo Awal = 12
        context.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            MovementNumber = "MV-PRIOR-01",
            MovementType = MovementType.GoodsReceipt,
            ProductId = prodA.Id,
            WarehouseId = wh1.Id,
            WarehouseLocationId = loc1.Id,
            Quantity = 20m,
            Direction = MovementDirection.In,
            UnitCost = 10_000_000m,
            TotalValue = 200_000_000m,
            SourceDocumentNumber = "GR-PRIOR-01",
            SourceDocumentType = SourceDocumentType.GoodsReceipt,
            SourceDocumentId = Guid.NewGuid(),
            UserId = user.Id,
            TimestampUtc = priorDateUtc,
            Status = MovementStatus.Posted
        });

        context.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            MovementNumber = "MV-PRIOR-02",
            MovementType = MovementType.GoodsIssue,
            ProductId = prodA.Id,
            WarehouseId = wh1.Id,
            WarehouseLocationId = loc1.Id,
            Quantity = 8m,
            Direction = MovementDirection.Out,
            UnitCost = 10_000_000m,
            TotalValue = 80_000_000m,
            SourceDocumentNumber = "SO-PRIOR-01",
            SourceDocumentType = SourceDocumentType.GoodsIssue,
            SourceDocumentId = Guid.NewGuid(),
            UserId = user.Id,
            TimestampUtc = priorDateUtc.AddHours(2),
            Status = MovementStatus.Posted
        });

        // 2. Mutasi DALAM periode filter: Masuk 10, Keluar 7
        var inPeriodDateUtc = JakartaTime.ToUtcStartOfDay(startOfMonth.AddDays(2));
        context.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            MovementNumber = "MV-CURR-01",
            MovementType = MovementType.GoodsReceipt,
            ProductId = prodA.Id,
            WarehouseId = wh1.Id,
            WarehouseLocationId = loc1.Id,
            Quantity = 10m,
            Direction = MovementDirection.In,
            UnitCost = 10_000_000m,
            TotalValue = 100_000_000m,
            SourceDocumentNumber = "GR-CURR-01",
            SourceDocumentType = SourceDocumentType.GoodsReceipt,
            SourceDocumentId = Guid.NewGuid(),
            UserId = user.Id,
            TimestampUtc = inPeriodDateUtc,
            Status = MovementStatus.Posted
        });

        context.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            MovementNumber = "MV-CURR-02",
            MovementType = MovementType.GoodsIssue,
            ProductId = prodA.Id,
            WarehouseId = wh1.Id,
            WarehouseLocationId = loc1.Id,
            Quantity = 7m,
            Direction = MovementDirection.Out,
            UnitCost = 10_000_000m,
            TotalValue = 70_000_000m,
            SourceDocumentNumber = "SO-CURR-01",
            SourceDocumentType = SourceDocumentType.GoodsIssue,
            SourceDocumentId = Guid.NewGuid(),
            UserId = user.Id,
            TimestampUtc = inPeriodDateUtc.AddHours(3),
            Status = MovementStatus.Posted
        });

        context.SaveChanges();

        var clock = new TestClock();
        var userContext = new TestUserContext();
        var service = new ReportService(context, userContext, clock);

        var result = await service.GetStockCardReportAsync(new StockCardQuery
        {
            ProductId = prodA.Id,
            WarehouseId = wh1.Id,
            StartDate = startOfMonth,
            EndDate = todayJakarta
        });

        result.Succeeded.Should().BeTrue();
        var card = result.Data!;

        // Verifikasi Saldo Awal (Opening Balance): 20 - 8 = 12
        card.OpeningBalance.Should().Be(12m);

        // Verifikasi Total Periode: In = 10, Out = 7
        card.TotalQuantityIn.Should().Be(10m);
        card.TotalQuantityOut.Should().Be(7m);

        // Verifikasi Saldo Akhir Historis: 12 + 10 - 7 = 15
        card.ClosingBalance.Should().Be(15m);

        // Verifikasi Saldo Fisik Saat Ini (Real-Time Current Stock): 15m
        card.CurrentRealTimeBalance.Should().Be(15m);

        // Verifikasi Running Balance baris per baris
        card.Lines.Should().HaveCount(2);
        card.Lines[0].QuantityIn.Should().Be(10m);
        card.Lines[0].RunningBalance.Should().Be(22m); // 12 + 10 = 22

        card.Lines[1].QuantityOut.Should().Be(7m);
        card.Lines[1].RunningBalance.Should().Be(15m); // 22 - 7 = 15
    }

    [Fact]
    public async Task ValuationReport_CurrentAndHistoricalModes_ShouldProduceAccurateResults()
    {
        using var context = CreateDbContext();
        var (wh1, wh2, prodA, prodB, user) = SeedBaseData(context);
        var loc1 = await context.WarehouseLocations.FirstAsync(l => l.WarehouseId == wh1.Id);

        // Simpan 1 movement ledger untuk prodA
        context.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            MovementNumber = "MV-VAL-01",
            MovementType = MovementType.GoodsReceipt,
            ProductId = prodA.Id,
            WarehouseId = wh1.Id,
            WarehouseLocationId = loc1.Id,
            Quantity = 15m,
            Direction = MovementDirection.In,
            UnitCost = 10_000_000m,
            TotalValue = 150_000_000m,
            SourceDocumentNumber = "GR-VAL-01",
            SourceDocumentType = SourceDocumentType.GoodsReceipt,
            SourceDocumentId = Guid.NewGuid(),
            UserId = user.Id,
            TimestampUtc = DateTime.UtcNow.AddDays(-1),
            Status = MovementStatus.Posted
        });
        context.SaveChanges();

        var clock = new TestClock();
        var userContext = new TestUserContext();
        var service = new ReportService(context, userContext, clock);

        // 1. Mode Current (Real-Time)
        var currentReport = await service.GetValuationReportAsync(new ValuationReportQuery
        {
            Mode = ValuationReportMode.Current
        });
        currentReport.TotalCount.Should().Be(2);
        var valSummary = await service.GetValuationReportSummaryAsync(new ValuationReportQuery
        {
            Mode = ValuationReportMode.Current
        });
        valSummary.Method.Should().Be("Rata-Rata Bergerak (Moving Average)");
        valSummary.TotalValuationValue.Should().Be(150_500_000m);

        // 2. Mode Historical (Rekonstruksi Ledger)
        var histReport = await service.GetValuationReportAsync(new ValuationReportQuery
        {
            Mode = ValuationReportMode.Historical,
            AsOfDate = JakartaTime.Today
        });
        histReport.TotalCount.Should().Be(1); // Hanya prodA yang punya movement
        histReport.Items[0].ProductId.Should().Be(prodA.Id);
        histReport.Items[0].OnHandQty.Should().Be(15m);
        histReport.Items[0].AvgCost.Should().Be(10_000_000m);
        histReport.Items[0].OnHandValue.Should().Be(150_000_000m);
    }

    [Fact]
    public async Task AuditLogService_ShouldFilterByActionAndSearch()
    {
        using var context = CreateDbContext();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "superadmin", FullName = "Super Admin", Email = "sa@wms.local" };
        context.Users.Add(user);

        context.AuditLogs.AddRange(
            new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                UserName = user.UserName,
                Action = "Create",
                EntityType = "Product",
                EntityId = "SKU-TEST-1",
                TimestampUtc = DateTime.UtcNow.AddMinutes(-30),
                CorrelationId = "corr-1"
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                UserName = user.UserName,
                Action = "Update",
                EntityType = "Product",
                EntityId = "SKU-TEST-1",
                TimestampUtc = DateTime.UtcNow.AddMinutes(-20),
                CorrelationId = "corr-2"
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                UserName = user.UserName,
                Action = "Post",
                EntityType = "GoodsReceipt",
                EntityId = "GR-202611-0001",
                TimestampUtc = DateTime.UtcNow.AddMinutes(-10),
                CorrelationId = "corr-3"
            }
        );
        context.SaveChanges();

        var service = new AuditLogService(context);

        // Filter by action "Create"
        var createLogs = await service.GetAuditLogsAsync(new AuditLogQuery { Action = "Create" });
        createLogs.TotalCount.Should().Be(1);
        createLogs.Items[0].Action.Should().Be("Create");

        // Filter by search "GoodsReceipt"
        var searchLogs = await service.GetAuditLogsAsync(new AuditLogQuery { Search = "GoodsReceipt" });
        searchLogs.TotalCount.Should().Be(1);
        searchLogs.Items[0].EntityType.Should().Be("GoodsReceipt");

        // Summary
        var summary = await service.GetAuditSummaryAsync();
        summary.TotalLogs.Should().Be(3);
        summary.TotalCreates.Should().Be(1);
        summary.TotalUpdates.Should().Be(1);
        summary.TotalPostings.Should().Be(1);
    }

    [Fact]
    public async Task MultiWarehouseIsolation_UserRestrictedToSingleWarehouse_ShouldNotSeeOtherWarehouses()
    {
        using var context = CreateDbContext();
        var (wh1, wh2, prodA, prodB, user) = SeedBaseData(context);

        // User restricted ONLY to wh2 (Surabaya)
        var restrictedUserContext = new TestUserContext
        {
            AllowedWarehouseIds = new[] { wh2.Id }
        };
        var clock = new TestClock();
        var reportService = new ReportService(context, restrictedUserContext, clock);

        var stockReport = await reportService.GetCurrentStockReportAsync(new StockReportQuery());

        // Wh2 has 0 stock in SeedBaseData (all stock was placed in Wh1)
        // User restricted to Wh2 should see 0 items from Wh1!
        stockReport.TotalCount.Should().Be(0);
        stockReport.Items.Should().BeEmpty();
    }
}
