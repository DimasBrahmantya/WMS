using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Wms.Domain.Authorization;
using Wms.Web.Authorization;
using Xunit;

namespace Wms.Integration.Tests;

public class PermissionPolicyProviderTests
{
    [Fact]
    public async Task GetPolicyAsync_WithDynamicPermission_ShouldReturnValidPolicyWithRequirement()
    {
        var options = Options.Create(new AuthorizationOptions());
        var provider = new PermissionPolicyProvider(options);

        var policy = await provider.GetPolicyAsync(Permissions.MasterProductView);

        policy.Should().NotBeNull();
        policy!.Requirements.OfType<PermissionRequirement>().Should().ContainSingle(pr => pr.Permission == Permissions.MasterProductView);
    }

    [Fact]
    public async Task GetDefaultPolicyAsync_ShouldReturnDefaultPolicy()
    {
        var options = Options.Create(new AuthorizationOptions());
        var provider = new PermissionPolicyProvider(options);

        var policy = await provider.GetDefaultPolicyAsync();

        policy.Should().NotBeNull();
        policy.Requirements.Should().NotBeEmpty();
    }
}
