using FluentAssertions;
using Wms.Domain.Common;
using Xunit;

namespace Wms.Domain.Tests.Common;

public class TestAuditableEntity : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
}

public class AuditableEntityTests
{
    [Fact]
    public void AuditableEntity_ShouldInitializeWithGuidAndUtcTimestamp()
    {
        // Act
        var entity = new TestAuditableEntity { Name = "Test" };

        // Assert
        entity.Id.Should().NotBeEmpty();
        entity.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entity.UpdatedAtUtc.Should().BeNull();
        entity.CreatedBy.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }
}
