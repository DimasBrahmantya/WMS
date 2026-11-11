using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Audit;
using Wms.Application.Features.SalesOrders;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Entities.Orders;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Batches;
using Wms.Infrastructure.Services.SalesOrders;
using Wms.Integration.Tests.Batches;
using Xunit;

namespace Wms.Integration.Tests.SalesOrders;

public class FakeDocumentSequenceService : IDocumentSequenceService
{
    private int _counter = 0;
    public Task<string> GetNextDocumentNumberAsync(string prefix, DateTime? date = null, CancellationToken cancellationToken = default)
    {
        _counter++;
        return Task.FromResult($"{prefix}-202611-{_counter:D6}");
    }
}

public class FakeAppAuditService : IAppAuditService
{
    public Task LogAsync(string entityType, string entityId, string action, Guid? userId, string? oldValues = null, string? newValues = null, string? reason = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class SalesOrderServiceTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    [Fact]
    public async Task CreateSalesOrderAsync_ShouldCreateOrderInDraftState()
    {
        using var context = CreateDbContext();
        var today = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock { TodayJakarta = today, UtcNow = today };

        var customerId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Users.Add(new ApplicationUser { Id = userId, UserName = "sales_user", FullName = "Sales User", Email = "sales@wms.local" });
        context.Customers.Add(new Customer { Id = customerId, Code = "C-01", Name = "Pelanggan 1", IsActive = true });
        context.Warehouses.Add(new Warehouse { Id = warehouseId, Code = "WH-01", Name = "Gudang 1", IsActive = true });
        context.Products.Add(new Product { Id = productId, Sku = "SKU-01", Name = "Barang 1", IsActive = true });
        await context.SaveChangesAsync();

        var seqService = new FakeDocumentSequenceService();
        var auditService = new FakeAppAuditService();
        var fefoService = new FefoService(context, clock);
        var service = new SalesOrderService(context, seqService, fefoService, auditService, clock);

        var cmd = new CreateSalesOrderCommand
        {
            CustomerId = customerId,
            WarehouseId = warehouseId,
            Lines = new List<CreateSalesOrderLineCommand>
            {
                new CreateSalesOrderLineCommand { ProductId = productId, OrderedQuantity = 10, UnitPrice = 15000 }
            }
        };

        var result = await service.CreateSalesOrderAsync(cmd, userId);
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.OrderNumber.Should().StartWith("SO-202611-");
        result.Data.Status.Should().Be(SalesOrderStatus.Draft);
        result.Data.TotalAmount.Should().Be(150000);
    }

    [Fact]
    public async Task ConfirmAndCancelSalesOrder_ShouldHandleStockReservationCorrectly()
    {
        using var context = CreateDbContext();
        var today = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock { TodayJakarta = today, UtcNow = today };

        var customerId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var locId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Users.Add(new ApplicationUser { Id = userId, UserName = "sales_user", FullName = "Sales User", Email = "sales@wms.local" });
        context.Customers.Add(new Customer { Id = customerId, Code = "C-01", Name = "Pelanggan 1", IsActive = true });
        context.Warehouses.Add(new Warehouse { Id = warehouseId, Code = "WH-01", Name = "Gudang 1", IsActive = true });
        context.WarehouseLocations.Add(new WarehouseLocation { Id = locId, WarehouseId = warehouseId, Code = "L-01", Name = "Loc 1", IsActive = true });
        context.Products.Add(new Product { Id = productId, Sku = "SKU-01", Name = "Barang 1", IsActive = true });

        // Initial Balance: 20 on hand, 0 reserved
        var balance = new InventoryBalance
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            WarehouseLocationId = locId,
            OnHand = 20,
            Reserved = 0,
            Status = StockStatus.Available
        };
        context.InventoryBalances.Add(balance);
        await context.SaveChangesAsync();

        var seqService = new FakeDocumentSequenceService();
        var auditService = new FakeAppAuditService();
        var fefoService = new FefoService(context, clock);
        var service = new SalesOrderService(context, seqService, fefoService, auditService, clock);

        var createResult = await service.CreateSalesOrderAsync(new CreateSalesOrderCommand
        {
            CustomerId = customerId,
            WarehouseId = warehouseId,
            Lines = new List<CreateSalesOrderLineCommand>
            {
                new CreateSalesOrderLineCommand { ProductId = productId, OrderedQuantity = 15, UnitPrice = 10000 }
            }
        }, userId);

        var soId = createResult.Data!.Id;

        // 1. Confirm: Reserves 15 units
        var confirmResult = await service.ConfirmSalesOrderAsync(soId, userId);
        confirmResult.Succeeded.Should().BeTrue();

        var updatedBalance = await context.InventoryBalances.FirstAsync();
        updatedBalance.OnHand.Should().Be(20);
        updatedBalance.Reserved.Should().Be(15);
        updatedBalance.Available.Should().Be(5); // 20 - 15 = 5

        // Attempting to reserve 10 more should fail because only 5 available!
        var createResult2 = await service.CreateSalesOrderAsync(new CreateSalesOrderCommand
        {
            CustomerId = customerId,
            WarehouseId = warehouseId,
            Lines = new List<CreateSalesOrderLineCommand>
            {
                new CreateSalesOrderLineCommand { ProductId = productId, OrderedQuantity = 10, UnitPrice = 10000 }
            }
        }, userId);

        var confirmResult2 = await service.ConfirmSalesOrderAsync(createResult2.Data!.Id, userId);
        confirmResult2.Succeeded.Should().BeFalse();
        confirmResult2.Errors.Should().Contain(e => e.Contains("tidak mencukupi"));

        // 2. Cancel: Releases the 15 reserved units
        var cancelResult = await service.CancelSalesOrderAsync(soId, userId, "Customer request");
        cancelResult.Succeeded.Should().BeTrue();

        var releasedBalance = await context.InventoryBalances.FirstAsync();
        releasedBalance.Reserved.Should().Be(0);
        releasedBalance.Available.Should().Be(20); // Restored!
    }

    [Fact]
    public async Task DispatchSalesOrder_ShouldDeductStockAndPostLedgerMovement()
    {
        using var context = CreateDbContext();
        var today = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock { TodayJakarta = today, UtcNow = today };

        var customerId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var locId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Users.Add(new ApplicationUser { Id = userId, UserName = "sales_user", FullName = "Sales User", Email = "sales@wms.local" });
        context.Customers.Add(new Customer { Id = customerId, Code = "C-01", Name = "Pelanggan 1", IsActive = true });
        context.Warehouses.Add(new Warehouse { Id = warehouseId, Code = "WH-01", Name = "Gudang 1", IsActive = true });
        context.WarehouseLocations.Add(new WarehouseLocation { Id = locId, WarehouseId = warehouseId, Code = "L-01", Name = "Loc 1", IsActive = true });
        context.Products.Add(new Product { Id = productId, Sku = "SKU-01", Name = "Barang 1", IsActive = true });

        context.InventoryBalances.Add(new InventoryBalance
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            WarehouseLocationId = locId,
            OnHand = 20,
            Reserved = 0,
            Status = StockStatus.Available
        });
        await context.SaveChangesAsync();

        var seqService = new FakeDocumentSequenceService();
        var auditService = new FakeAppAuditService();
        var fefoService = new FefoService(context, clock);
        var service = new SalesOrderService(context, seqService, fefoService, auditService, clock);

        var createResult = await service.CreateSalesOrderAsync(new CreateSalesOrderCommand
        {
            CustomerId = customerId,
            WarehouseId = warehouseId,
            Lines = new List<CreateSalesOrderLineCommand>
            {
                new CreateSalesOrderLineCommand { ProductId = productId, OrderedQuantity = 12, UnitPrice = 10000 }
            }
        }, userId);

        var soId = createResult.Data!.Id;
        await service.ConfirmSalesOrderAsync(soId, userId);

        // Dispatch
        var dispatchResult = await service.DispatchSalesOrderAsync(new DispatchSalesOrderCommand
        {
            SalesOrderId = soId,
            UserId = userId
        });

        dispatchResult.Succeeded.Should().BeTrue();

        var finalBalance = await context.InventoryBalances.FirstAsync();
        finalBalance.OnHand.Should().Be(8);     // 20 - 12 = 8
        finalBalance.Reserved.Should().Be(0);   // Released upon physical dispatch
        finalBalance.Available.Should().Be(8);

        // Verify StockMovement in ledger
        var movement = await context.StockMovements.FirstOrDefaultAsync();
        movement.Should().NotBeNull();
        movement!.MovementType.Should().Be(MovementType.SalesOrderIssue);
        movement.Quantity.Should().Be(12);
        movement.Direction.Should().Be(MovementDirection.Out);
        movement.BalanceBefore.Should().Be(20);
        movement.BalanceAfter.Should().Be(8);
    }
}

