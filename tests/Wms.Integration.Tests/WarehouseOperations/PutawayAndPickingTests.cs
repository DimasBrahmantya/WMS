using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Entities.Orders;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Batches;
using Wms.Infrastructure.Services.WarehouseOperations;
using Wms.Integration.Tests.Batches;
using Xunit;

namespace Wms.Integration.Tests.WarehouseOperations;

public class PutawayAndPickingTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    [Fact]
    public async Task SuggestPutawayLocationAsync_WithExistingStock_ShouldSuggestExistingLocation()
    {
        using var context = CreateDbContext();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var locExisting = Guid.NewGuid();
        var locOther = Guid.NewGuid();

        context.WarehouseLocations.AddRange(
            new WarehouseLocation { Id = locExisting, WarehouseId = warehouseId, Code = "LOC-01", Name = "Rak A1", IsActive = true },
            new WarehouseLocation { Id = locOther, WarehouseId = warehouseId, Code = "LOC-02", Name = "Rak A2", IsActive = true }
        );

        context.InventoryBalances.Add(new InventoryBalance
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            WarehouseLocationId = locExisting,
            OnHand = 50,
            Reserved = 0,
            Status = StockStatus.Available
        });

        await context.SaveChangesAsync();

        var service = new PutawayService(context);
        var suggestion = await service.SuggestPutawayLocationAsync(productId, warehouseId);

        suggestion.Should().NotBeNull();
        suggestion!.WarehouseLocationId.Should().Be(locExisting);
        suggestion.Strategy.Should().Be("SameProductExistingLocation");
    }

    [Fact]
    public async Task SuggestPutawayLocationAsync_WithNoStock_ShouldSuggestEmptyBin()
    {
        using var context = CreateDbContext();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var locEmpty = Guid.NewGuid();

        context.WarehouseLocations.Add(
            new WarehouseLocation { Id = locEmpty, WarehouseId = warehouseId, Code = "EMPTY-01", Name = "Rak Kosong", IsActive = true }
        );

        await context.SaveChangesAsync();

        var service = new PutawayService(context);
        var suggestion = await service.SuggestPutawayLocationAsync(productId, warehouseId);

        suggestion.Should().NotBeNull();
        suggestion!.WarehouseLocationId.Should().Be(locEmpty);
        suggestion.Strategy.Should().Be("EmptyBin");
    }

    [Fact]
    public async Task GeneratePickingListForSalesOrderAsync_ShouldGenerateFEFOBasedList()
    {
        using var context = CreateDbContext();
        var today = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock { TodayJakarta = today, UtcNow = today };

        var warehouseId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var locId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "staff_picking",
            FullName = "Staff Picking",
            Email = "picking@wms.local"
        });

        context.Warehouses.Add(new Warehouse
        {
            Id = warehouseId,
            Code = "WH-01",
            Name = "Gudang Utama",
            IsActive = true
        });

        context.WarehouseLocations.Add(new WarehouseLocation
        {
            Id = locId,
            WarehouseId = warehouseId,
            Code = "PICK-01",
            Name = "Pick Bin 1",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = customerId,
            Code = "CUST-001",
            Name = "PT Pelanggan Maju",
            IsActive = true
        });

        context.Products.Add(new Product
        {
            Id = productId,
            Sku = "SKU-PICK-01",
            Name = "Barang Siap Kirim",
            IsActive = true
        });

        context.Batches.Add(new Batch
        {
            ProductId = productId,
            BatchNumber = "BATCH-FEFO",
            ExpiryDate = new DateOnly(2026, 11, 25),
            IsBlocked = false
        });

        context.InventoryBalances.Add(new InventoryBalance
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            WarehouseLocationId = locId,
            BatchNumber = "BATCH-FEFO",
            OnHand = 20,
            Reserved = 0,
            Status = StockStatus.Available
        });

        var soId = Guid.NewGuid();
        var so = new SalesOrder
        {
            Id = soId,
            OrderNumber = "SO-202611-000001",
            CustomerId = customerId,
            WarehouseId = warehouseId,
            CreatedByUserId = userId,
            Status = SalesOrderStatus.Confirmed,
            Lines = new List<SalesOrderLine>
            {
                new SalesOrderLine
                {
                    ProductId = productId,
                    OrderedQuantity = 5,
                    UnitPrice = 10000
                }
            }
        };
        context.SalesOrders.Add(so);

        await context.SaveChangesAsync();

        var fefoService = new FefoService(context, clock);
        var pickingService = new PickingService(context, fefoService);

        var pickingList = await pickingService.GeneratePickingListForSalesOrderAsync(soId);

        pickingList.Should().NotBeNull();
        pickingList.OrderNumber.Should().Be("SO-202611-000001");
        pickingList.Lines.Should().HaveCount(1);
        pickingList.Lines[0].Allocations.Should().HaveCount(1);
        pickingList.Lines[0].Allocations[0].BatchNumber.Should().Be("BATCH-FEFO");
        pickingList.Lines[0].Allocations[0].QuantityToPick.Should().Be(5);
    }
}

