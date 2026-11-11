using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Domain.Common;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Batches;
using Xunit;

namespace Wms.Integration.Tests.Batches;

public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    public DateTime TodayJakarta { get; set; } = DateTime.UtcNow;
}

public class FefoServiceTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    [Fact]
    public async Task AllocateFefoAsync_ShouldPrioritizeEarliestExpiryDate()
    {
        using var context = CreateDbContext();
        var today = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock { TodayJakarta = today, UtcNow = today };

        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        context.WarehouseLocations.Add(new WarehouseLocation
        {
            Id = locId,
            WarehouseId = warehouseId,
            Code = "A-01-01",
            Name = "Bin 1"
        });

        // 3 Batches with different expiries
        context.Batches.AddRange(
            new Batch { ProductId = productId, BatchNumber = "B-DEC", ExpiryDate = new DateOnly(2026, 12, 1) },
            new Batch { ProductId = productId, BatchNumber = "B-NOV", ExpiryDate = new DateOnly(2026, 11, 20) }, // Earliest
            new Batch { ProductId = productId, BatchNumber = "B-JAN", ExpiryDate = new DateOnly(2027, 1, 15) }
        );

        // Balances
        context.InventoryBalances.AddRange(
            new InventoryBalance { ProductId = productId, WarehouseId = warehouseId, WarehouseLocationId = locId, BatchNumber = "B-DEC", OnHand = 10, Reserved = 0, Status = StockStatus.Available },
            new InventoryBalance { ProductId = productId, WarehouseId = warehouseId, WarehouseLocationId = locId, BatchNumber = "B-NOV", OnHand = 10, Reserved = 0, Status = StockStatus.Available },
            new InventoryBalance { ProductId = productId, WarehouseId = warehouseId, WarehouseLocationId = locId, BatchNumber = "B-JAN", OnHand = 10, Reserved = 0, Status = StockStatus.Available }
        );

        await context.SaveChangesAsync();

        var service = new FefoService(context, clock);

        // Request 15 units
        var allocations = await service.AllocateFefoAsync(productId, warehouseId, 15);

        allocations.Should().HaveCount(2);
        allocations[0].BatchNumber.Should().Be("B-NOV");
        allocations[0].AllocatedQuantity.Should().Be(10);
        allocations[1].BatchNumber.Should().Be("B-DEC");
        allocations[1].AllocatedQuantity.Should().Be(5);
    }

    [Fact]
    public async Task AllocateFefoAsync_ShouldSkipExpiredAndBlockedBatches()
    {
        using var context = CreateDbContext();
        var today = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock { TodayJakarta = today, UtcNow = today };

        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        context.WarehouseLocations.Add(new WarehouseLocation
        {
            Id = locId,
            WarehouseId = warehouseId,
            Code = "A-01-01",
            Name = "Bin 1"
        });

        // Batches: one expired, one blocked, one valid
        context.Batches.AddRange(
            new Batch { ProductId = productId, BatchNumber = "B-EXP", ExpiryDate = new DateOnly(2026, 11, 10), IsBlocked = false }, // Yesterday
            new Batch { ProductId = productId, BatchNumber = "B-BLK", ExpiryDate = new DateOnly(2026, 11, 25), IsBlocked = true, BlockReason = "Damage inspection" },
            new Batch { ProductId = productId, BatchNumber = "B-VALID", ExpiryDate = new DateOnly(2026, 11, 30), IsBlocked = false }
        );

        context.InventoryBalances.AddRange(
            new InventoryBalance { ProductId = productId, WarehouseId = warehouseId, WarehouseLocationId = locId, BatchNumber = "B-EXP", OnHand = 10, Reserved = 0, Status = StockStatus.Available },
            new InventoryBalance { ProductId = productId, WarehouseId = warehouseId, WarehouseLocationId = locId, BatchNumber = "B-BLK", OnHand = 10, Reserved = 0, Status = StockStatus.Available },
            new InventoryBalance { ProductId = productId, WarehouseId = warehouseId, WarehouseLocationId = locId, BatchNumber = "B-VALID", OnHand = 10, Reserved = 0, Status = StockStatus.Available }
        );

        await context.SaveChangesAsync();

        var service = new FefoService(context, clock);

        // Request 15 units
        var allocations = await service.AllocateFefoAsync(productId, warehouseId, 15);

        allocations.Should().HaveCount(1);
        allocations[0].BatchNumber.Should().Be("B-VALID");
        allocations[0].AllocatedQuantity.Should().Be(10);
    }

    [Fact]
    public async Task IsBatchExpiredOrBlockedAsync_ShouldReturnTrueForBlockedOrExpired()
    {
        using var context = CreateDbContext();
        var today = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock { TodayJakarta = today, UtcNow = today };

        var productId = Guid.NewGuid();
        context.Batches.AddRange(
            new Batch { ProductId = productId, BatchNumber = "B-EXP", ExpiryDate = new DateOnly(2026, 11, 5) },
            new Batch { ProductId = productId, BatchNumber = "B-OK", ExpiryDate = new DateOnly(2026, 12, 5) }
        );
        await context.SaveChangesAsync();

        var service = new FefoService(context, clock);

        (await service.IsBatchExpiredOrBlockedAsync(productId, "B-EXP")).Should().BeTrue();
        (await service.IsBatchExpiredOrBlockedAsync(productId, "B-OK")).Should().BeFalse();
        (await service.IsBatchExpiredOrBlockedAsync(productId, null)).Should().BeFalse();
    }
}

