using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Stock;
using Xunit;

namespace Wms.Integration.Tests.Stock;

public class StockReconciliationTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    [Fact]
    public async Task ReconcileAsync_WhenBalancesMatchLedger_ShouldReturnIsConsistent()
    {
        using var context = CreateDbContext();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin", FullName = "Admin", Email = "admin@wms.local" };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Code = "WH-01", Name = "Gudang Utama", IsActive = true };
        var location = new WarehouseLocation { Id = Guid.NewGuid(), WarehouseId = warehouse.Id, Code = "LOC-01", Name = "Lokasi 1", IsActive = true };
        var category = new Category { Id = Guid.NewGuid(), Name = "Elektronik", IsActive = true };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Pcs", Abbreviation = "PCS", IsActive = true };
        var product = new Product { Id = Guid.NewGuid(), Sku = "SKU-001", Name = "Test Product", CategoryId = category.Id, UnitId = unit.Id, IsActive = true };

        context.Users.Add(user);
        context.Warehouses.Add(warehouse);
        context.WarehouseLocations.Add(location);
        context.Categories.Add(category);
        context.Units.Add(unit);
        context.Products.Add(product);

        // Saldo 10
        context.InventoryBalances.Add(new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            Status = StockStatus.Available,
            OnHand = 10m
        });

        // Ledger: In 15, Out 5 => sum = 10
        context.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            MovementNumber = "MV-202611-000001",
            MovementType = MovementType.GoodsReceipt,
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            StockStatus = StockStatus.Available,
            Quantity = 15m,
            Direction = MovementDirection.In,
            SourceDocumentType = SourceDocumentType.GoodsReceipt,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentNumber = "GR-001",
            UserId = user.Id,
            Status = MovementStatus.Posted
        });
        context.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            MovementNumber = "MV-202611-000002",
            MovementType = MovementType.GoodsIssue,
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            StockStatus = StockStatus.Available,
            Quantity = 5m,
            Direction = MovementDirection.Out,
            SourceDocumentType = SourceDocumentType.GoodsIssue,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentNumber = "GI-001",
            UserId = user.Id,
            Status = MovementStatus.Posted
        });

        await context.SaveChangesAsync();

        var service = new StockReconciliationService(context);
        var report = await service.ReconcileAsync();

        report.IsConsistent.Should().BeTrue();
        report.DiscrepancyCount.Should().Be(0);
    }

    [Fact]
    public async Task ReconcileAsync_WhenDiscrepancyExists_ShouldDetectDifference()
    {
        using var context = CreateDbContext();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin", FullName = "Admin", Email = "admin@wms.local" };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Code = "WH-01", Name = "Gudang Utama", IsActive = true };
        var location = new WarehouseLocation { Id = Guid.NewGuid(), WarehouseId = warehouse.Id, Code = "LOC-01", Name = "Lokasi 1", IsActive = true };
        var category = new Category { Id = Guid.NewGuid(), Name = "Elektronik", IsActive = true };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Pcs", Abbreviation = "PCS", IsActive = true };
        var product = new Product { Id = Guid.NewGuid(), Sku = "SKU-001", Name = "Test Product", CategoryId = category.Id, UnitId = unit.Id, IsActive = true };

        context.Users.Add(user);
        context.Warehouses.Add(warehouse);
        context.WarehouseLocations.Add(location);
        context.Categories.Add(category);
        context.Units.Add(unit);
        context.Products.Add(product);

        // Saldo tercatat 12 (selisih 2 dengan ledger yang bernilai 10)
        context.InventoryBalances.Add(new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            Status = StockStatus.Available,
            OnHand = 12m
        });

        context.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            MovementNumber = "MV-202611-000001",
            MovementType = MovementType.GoodsReceipt,
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            StockStatus = StockStatus.Available,
            Quantity = 10m,
            Direction = MovementDirection.In,
            SourceDocumentType = SourceDocumentType.GoodsReceipt,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentNumber = "GR-001",
            UserId = user.Id,
            Status = MovementStatus.Posted
        });

        await context.SaveChangesAsync();

        var service = new StockReconciliationService(context);
        var report = await service.ReconcileAsync();

        report.IsConsistent.Should().BeFalse();
        report.DiscrepancyCount.Should().Be(1);
        report.Discrepancies[0].BalanceOnHand.Should().Be(12m);
        report.Discrepancies[0].LedgerOnHand.Should().Be(10m);
        report.Discrepancies[0].Difference.Should().Be(2m);
    }
}

