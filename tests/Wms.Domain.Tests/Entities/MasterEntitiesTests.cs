using FluentAssertions;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;
using Xunit;

namespace Wms.Domain.Tests.Entities;

public class MasterEntitiesTests
{
    [Fact]
    public void Product_Creation_ShouldHaveDefaultValues()
    {
        var product = new Product
        {
            Sku = "SKU-001",
            Name = "Test Product",
            CategoryId = Guid.NewGuid(),
            UnitId = Guid.NewGuid()
        };

        product.IsActive.Should().BeTrue();
        product.TrackingMethod.Should().Be(TrackingMethod.None);
        product.MinStock.Should().Be(0);
        product.MaxStock.Should().BeNull();
        product.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Warehouse_Creation_ShouldInitializeLocationsList()
    {
        var warehouse = new Warehouse
        {
            Code = "WH-01",
            Name = "Gudang Utama"
        };

        warehouse.Locations.Should().NotBeNull();
        warehouse.IsActive.Should().BeTrue();
    }
}
