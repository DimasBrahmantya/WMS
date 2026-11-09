using FluentAssertions;
using Wms.Domain.Authorization;
using Xunit;

namespace Wms.Domain.Tests.Authorization;

public class PermissionsTests
{
    [Fact]
    public void Permissions_All_ShouldContainUniquePermissions()
    {
        // Act
        var allPermissions = Permissions.All;

        // Assert
        allPermissions.Should().NotBeNullOrEmpty();
        allPermissions.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(Permissions.UsersView)]
    [InlineData(Permissions.StockInPost)]
    [InlineData(Permissions.StockOutPost)]
    [InlineData(Permissions.AuditView)]
    public void Permissions_ShouldFollowDotNotationFormat(string permission)
    {
        // Assert
        permission.Should().Contain(".");
        permission.Should().Be(permission.ToLowerInvariant());
    }
}
