using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Infrastructure.Persistence;
using Xunit;

namespace Wms.Integration.Tests;

public class FoundationIntegrationTests
{
    [Fact]
    public void WmsDbContext_Model_ShouldBeConfiguredProperly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new WmsDbContext(options);
        var model = context.Model;

        // Assert
        model.Should().NotBeNull();
        model.FindEntityType(typeof(Wms.Domain.Entities.Identity.ApplicationUser)).Should().NotBeNull();
        model.FindEntityType(typeof(Wms.Domain.Entities.Identity.ApplicationRole)).Should().NotBeNull();
        model.FindEntityType(typeof(Wms.Domain.Entities.System.AuditLog)).Should().NotBeNull();
        model.FindEntityType(typeof(Wms.Domain.Entities.System.AppSetting)).Should().NotBeNull();
    }
}
