using FluentAssertions;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Orders;
using Wms.Domain.Entities.System;
using Wms.Domain.Enums;
using Xunit;

namespace Wms.Domain.Tests.Entities;

public class Tahap7EntitiesTests
{
    [Fact]
    public void Batch_ExpiryAndBlockingCheck_ShouldBehaveCorrectly()
    {
        var today = new DateOnly(2026, 11, 11);
        var validBatch = new Batch
        {
            BatchNumber = "BATCH-001",
            ExpiryDate = today.AddDays(30),
            IsBlocked = false
        };

        validBatch.IsExpired(today).Should().BeFalse();
        validBatch.CanBeIssued(today).Should().BeTrue();

        var expiredBatch = new Batch
        {
            BatchNumber = "BATCH-002",
            ExpiryDate = today.AddDays(-1),
            IsBlocked = false
        };

        expiredBatch.IsExpired(today).Should().BeTrue();
        expiredBatch.CanBeIssued(today).Should().BeFalse();

        var blockedBatch = new Batch
        {
            BatchNumber = "BATCH-003",
            ExpiryDate = today.AddDays(30),
            IsBlocked = true,
            BlockReason = "Quality inspection required"
        };

        blockedBatch.IsExpired(today).Should().BeFalse();
        blockedBatch.CanBeIssued(today).Should().BeFalse();
    }

    [Fact]
    public void SalesOrder_DefaultState_ShouldBeDraft()
    {
        var so = new SalesOrder
        {
            OrderNumber = "SO-202611-000001",
            CustomerId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid()
        };

        so.Status.Should().Be(SalesOrderStatus.Draft);
        so.Lines.Should().BeEmpty();
        so.TotalAmount.Should().Be(0);
    }

    [Fact]
    public void SalesOrderLine_Calculations_ShouldBeAccurate()
    {
        var line = new SalesOrderLine
        {
            OrderedQuantity = 10,
            UnitPrice = 25000,
            ReservedQuantity = 10,
            DispatchedQuantity = 4
        };

        line.Subtotal.Should().Be(250000);
        line.RemainingQuantity.Should().Be(6);
    }

    [Fact]
    public void ApiKey_ValidationAndScopes_ShouldWorkCorrectly()
    {
        var now = DateTime.UtcNow;
        var apiKey = new ApiKey
        {
            Name = "ERP Integration",
            KeyPrefix = "wms_live_1234",
            KeyHash = "hash123",
            Scopes = $"{ApiKeyScope.ProductsRead}, {ApiKeyScope.SalesOrdersWrite}",
            ExpiresAtUtc = now.AddDays(30),
            IsActive = true
        };

        apiKey.IsValid(now).Should().BeTrue();
        apiKey.IsExpired(now).Should().BeFalse();
        apiKey.HasScope(ApiKeyScope.ProductsRead).Should().BeTrue();
        apiKey.HasScope(ApiKeyScope.SalesOrdersWrite).Should().BeTrue();
        apiKey.HasScope(ApiKeyScope.InventoryRead).Should().BeFalse();

        // Expired check
        var expiredKey = new ApiKey
        {
            ExpiresAtUtc = now.AddDays(-1),
            IsActive = true
        };
        expiredKey.IsValid(now).Should().BeFalse();

        // Deactivated check
        var inactiveKey = new ApiKey
        {
            ExpiresAtUtc = now.AddDays(10),
            IsActive = false
        };
        inactiveKey.IsValid(now).Should().BeFalse();
    }
}

