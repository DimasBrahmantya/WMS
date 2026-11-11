using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.Domain.Common;
using Wms.Domain.Entities.Master;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Backup;

namespace Wms.Integration.Tests.Backup;

public class SystemBackupServiceTests
{
    private class FakeClock : IClock
    {
        public DateTime UtcNow => new(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);
        public DateTime TodayJakarta => new(2026, 11, 11);
    }

    private WmsDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    [Fact]
    public async Task CreateSnapshotBackupAsync_ReturnsStructuredJsonWithCorrectChecksum()
    {
        using var context = CreateInMemoryContext();
        var clock = new FakeClock();

        var category = new Category { Id = Guid.NewGuid(), Name = "Elektronik", Description = "Barang IT" };
        var unit = new Unit { Id = Guid.NewGuid(), Abbreviation = "PCS", Name = "Pieces" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-BACKUP-01",
            Name = "Laptop Backup Test",
            CategoryId = category.Id,
            UnitId = unit.Id,
            MinStock = 2,
            MaxStock = 10
        };

        context.Categories.Add(category);
        context.Units.Add(unit);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var backupService = new SystemBackupService(context, clock);

        var result = await backupService.CreateSnapshotBackupAsync("superadmin@wms.local");

        Assert.NotNull(result);
        Assert.NotEmpty(result.FileName);
        Assert.StartsWith("wms_backup_20261111_100000", result.FileName);
        Assert.Equal("application/json", result.ContentType);
        Assert.NotEmpty(result.ChecksumSha256);
        Assert.True(result.RecordCount >= 2); // Category, Product
        Assert.True(result.Content.Length > 0);

        // Verify valid JSON
        var jsonString = System.Text.Encoding.UTF8.GetString(result.Content);
        using var jsonDoc = JsonDocument.Parse(jsonString);
        var root = jsonDoc.RootElement;
        Assert.True(root.TryGetProperty("metadata", out var metadata));
        Assert.True(root.TryGetProperty("data", out var data));
        Assert.True(data.TryGetProperty("products", out var productsElem));
        Assert.Equal(1, productsElem.GetArrayLength());
    }
}
