using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Domain.Common;
using Wms.Domain.Entities.Master;
using Wms.Infrastructure.Persistence;
using Xunit;

namespace Wms.Integration.Tests;

public class WarehouseGlobalQueryFilterTests
{
    private class FakeUserContext : IUserContext
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string? Username { get; set; } = "testuser";
        public string? FullName { get; set; } = "Test User";
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<Guid> AllowedWarehouseIds { get; set; } = Array.Empty<Guid>();
        public bool IsAuthenticated { get; set; } = true;

        public bool HasPermission(string permission) => Roles.Contains("SuperAdmin") || Permissions.Contains(permission);
        public bool CanAccessWarehouse(Guid warehouseId) => Roles.Contains("SuperAdmin") || AllowedWarehouseIds.Count == 0 || AllowedWarehouseIds.Contains(warehouseId);
    }

    [Fact]
    public async Task GlobalQueryFilter_SuperAdmin_ShouldSeeAllWarehousesAndLocations()
    {
        var dbName = Guid.NewGuid().ToString();
        var wh1Id = Guid.NewGuid();
        var wh2Id = Guid.NewGuid();

        // 1. Seed data with admin
        var adminContext = new FakeUserContext
        {
            Roles = new[] { "SuperAdmin" },
            AllowedWarehouseIds = Array.Empty<Guid>()
        };

        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using (var seedContext = new WmsDbContext(options, adminContext))
        {
            seedContext.Warehouses.AddRange(
                new Warehouse { Id = wh1Id, Code = "WH-1", Name = "Gudang 1", IsActive = true, CreatedAtUtc = DateTime.UtcNow },
                new Warehouse { Id = wh2Id, Code = "WH-2", Name = "Gudang 2", IsActive = true, CreatedAtUtc = DateTime.UtcNow }
            );

            seedContext.WarehouseLocations.AddRange(
                new WarehouseLocation { Id = Guid.NewGuid(), WarehouseId = wh1Id, Code = "L-1", Name = "Lokasi 1", IsActive = true, CreatedAtUtc = DateTime.UtcNow },
                new WarehouseLocation { Id = Guid.NewGuid(), WarehouseId = wh2Id, Code = "L-2", Name = "Lokasi 2", IsActive = true, CreatedAtUtc = DateTime.UtcNow }
            );

            await seedContext.SaveChangesAsync();
        }

        // 2. Query as SuperAdmin
        using (var queryContext = new WmsDbContext(options, adminContext))
        {
            var warehouses = await queryContext.Warehouses.ToListAsync();
            var locations = await queryContext.WarehouseLocations.ToListAsync();

            warehouses.Should().HaveCount(2);
            locations.Should().HaveCount(2);
        }
    }

    [Fact]
    public async Task GlobalQueryFilter_RestrictedUser_ShouldOnlySeeAssignedWarehouseAndLocations()
    {
        var dbName = Guid.NewGuid().ToString();
        var wh1Id = Guid.NewGuid();
        var wh2Id = Guid.NewGuid();
        var loc1Id = Guid.NewGuid();
        var loc2Id = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // 1. Seed data
        using (var seedContext = new WmsDbContext(options, null))
        {
            seedContext.Warehouses.AddRange(
                new Warehouse { Id = wh1Id, Code = "WH-1", Name = "Gudang 1 (Diizinkan)", IsActive = true, CreatedAtUtc = DateTime.UtcNow },
                new Warehouse { Id = wh2Id, Code = "WH-2", Name = "Gudang 2 (Terlarang)", IsActive = true, CreatedAtUtc = DateTime.UtcNow }
            );

            seedContext.WarehouseLocations.AddRange(
                new WarehouseLocation { Id = loc1Id, WarehouseId = wh1Id, Code = "L-1", Name = "Lokasi WH-1", IsActive = true, CreatedAtUtc = DateTime.UtcNow },
                new WarehouseLocation { Id = loc2Id, WarehouseId = wh2Id, Code = "L-2", Name = "Lokasi WH-2", IsActive = true, CreatedAtUtc = DateTime.UtcNow }
            );

            await seedContext.SaveChangesAsync();
        }

        // 2. Query as restricted user (only WH-1 assigned)
        var restrictedContext = new FakeUserContext
        {
            Roles = new[] { "PetugasGudang" },
            AllowedWarehouseIds = new[] { wh1Id }
        };

        using (var queryContext = new WmsDbContext(options, restrictedContext))
        {
            var warehouses = await queryContext.Warehouses.ToListAsync();
            warehouses.Should().HaveCount(1);
            warehouses[0].Id.Should().Be(wh1Id);

            var locations = await queryContext.WarehouseLocations.ToListAsync();
            locations.Should().HaveCount(1);
            locations[0].Id.Should().Be(loc1Id);

            // Directly querying restricted warehouse should return null
            var forbiddenWarehouse = await queryContext.Warehouses.FirstOrDefaultAsync(w => w.Id == wh2Id);
            forbiddenWarehouse.Should().BeNull();

            // Directly querying restricted location should return null
            var forbiddenLocation = await queryContext.WarehouseLocations.FirstOrDefaultAsync(l => l.Id == loc2Id);
            forbiddenLocation.Should().BeNull();

            // With IgnoreQueryFilters, all records are visible (for system/admin use)
            var allWarehouses = await queryContext.Warehouses.IgnoreQueryFilters().ToListAsync();
            allWarehouses.Should().HaveCount(2);
        }
    }
}

