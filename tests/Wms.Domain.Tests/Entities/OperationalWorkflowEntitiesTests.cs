using FluentAssertions;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;
using Xunit;

namespace Wms.Domain.Tests.Entities;

public class OperationalWorkflowEntitiesTests
{
    [Fact]
    public void PurchaseOrder_Creation_ShouldInitializeDefaultValues()
    {
        var po = new PurchaseOrder
        {
            PoNumber = "PO-202611-000001",
            SupplierId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid()
        };

        po.Status.Should().Be(PurchaseOrderStatus.Draft);
        po.Lines.Should().NotBeNull();
        po.Lines.Should().BeEmpty();
        po.OrderDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StockTransfer_Creation_ShouldInitializeDefaultValues()
    {
        var transfer = new StockTransfer
        {
            TransferNumber = "TRF-202611-000001",
            OriginWarehouseId = Guid.NewGuid(),
            DestinationWarehouseId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid()
        };

        transfer.Status.Should().Be(StockTransferStatus.Draft);
        transfer.Lines.Should().NotBeNull();
        transfer.Lines.Should().BeEmpty();
    }

    [Fact]
    public void StockOpnameSession_Creation_ShouldInitializeDefaultValues()
    {
        var session = new StockOpnameSession
        {
            SessionNumber = "OPN-202611-000001",
            WarehouseId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid()
        };

        session.Status.Should().Be(StockOpnameStatus.Draft);
        session.Lines.Should().NotBeNull();
        session.Lines.Should().BeEmpty();
    }

    [Fact]
    public void StockIncident_Creation_ShouldInitializeDefaultValues()
    {
        var incident = new StockIncident
        {
            IncidentNumber = "INC-202611-000001",
            WarehouseId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 2,
            Category = StockIncidentCategory.Lost,
            ReportedByUserId = Guid.NewGuid()
        };

        incident.Status.Should().Be(StockIncidentStatus.Reported);
        incident.ReportedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StockReturn_Creation_ShouldInitializeDefaultValues()
    {
        var ret = new StockReturn
        {
            ReturnNumber = "RET-202611-000001",
            WarehouseId = Guid.NewGuid(),
            ReturnType = StockReturnType.ReturnToSupplier,
            CreatedByUserId = Guid.NewGuid()
        };

        ret.Status.Should().Be(StockReturnStatus.Draft);
        ret.Lines.Should().NotBeNull();
    }
}
