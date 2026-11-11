using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.ApiKeys;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.ApiKeys;
using Wms.Integration.Tests.Batches;
using Xunit;

namespace Wms.Integration.Tests.Api;

public class ApiKeyServiceTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    [Fact]
    public async Task CreateApiKeyAsync_ShouldGenerateKeyWithPrefixAndHash()
    {
        using var context = CreateDbContext();
        var clock = new FakeClock();
        var service = new ApiKeyService(context, clock);

        var cmd = new CreateApiKeyCommand
        {
            Name = "ERP Integration",
            Scopes = new List<string> { ApiKeyScope.ProductsRead, ApiKeyScope.SalesOrdersWrite },
            ExpiresAtUtc = DateTime.UtcNow.AddDays(90)
        };

        var result = await service.CreateApiKeyAsync(cmd);
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PlainSecretKey.Should().StartWith("wms_live_");
        result.Data.ApiKey.KeyPrefix.Should().Be(result.Data.PlainSecretKey.Substring(0, 16));
        result.Data.ApiKey.Name.Should().Be("ERP Integration");
        result.Data.ApiKey.HasScope(ApiKeyScope.ProductsRead).Should().BeTrue();
        result.Data.ApiKey.HasScope(ApiKeyScope.SalesOrdersWrite).Should().BeTrue();
        result.Data.ApiKey.HasScope(ApiKeyScope.InventoryRead).Should().BeFalse();
    }

    [Fact]
    public async Task ValidateKeyAsync_WithValidKey_ShouldReturnDto()
    {
        using var context = CreateDbContext();
        var clock = new FakeClock();
        var service = new ApiKeyService(context, clock);

        var createResult = await service.CreateApiKeyAsync(new CreateApiKeyCommand
        {
            Name = "POS Client",
            Scopes = new List<string> { ApiKeyScope.InventoryRead }
        });

        var plainKey = createResult.Data!.PlainSecretKey;

        var validated = await service.ValidateKeyAsync(plainKey);
        validated.Should().NotBeNull();
        validated!.Name.Should().Be("POS Client");

        // Verify invalid key returns null
        var invalid = await service.ValidateKeyAsync("wms_live_invalidkey123");
        invalid.Should().BeNull();
    }

    [Fact]
    public async Task RevokeApiKeyAsync_ShouldPreventValidation()
    {
        using var context = CreateDbContext();
        var clock = new FakeClock();
        var service = new ApiKeyService(context, clock);

        var createResult = await service.CreateApiKeyAsync(new CreateApiKeyCommand
        {
            Name = "Revocable Key",
            Scopes = new List<string> { ApiKeyScope.ProductsRead }
        });

        var keyId = createResult.Data!.ApiKey.Id;
        var plainKey = createResult.Data.PlainSecretKey;

        // Revoke
        var revokeResult = await service.RevokeApiKeyAsync(keyId);
        revokeResult.Succeeded.Should().BeTrue();

        // Validate should now return null
        var validated = await service.ValidateKeyAsync(plainKey);
        validated.Should().BeNull();
    }
}

