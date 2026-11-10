using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Operations.Transfers;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Application.Features.Stock.Idempotency;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Audit;
using Wms.Infrastructure.Services.Operations;
using Wms.Infrastructure.Services.Stock;
using Xunit;

namespace Wms.Integration.Tests.Operations;

public class StockTransferTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    private (WmsDbContext context, Warehouse originWh, WarehouseLocation originLoc, Warehouse destWh, WarehouseLocation destLoc, Product product, ApplicationUser creator, ApplicationUser approver) SeedTestData(WmsDbContext context)
    {
        var creator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "creator_user",
            FullName = "Admin Asal",
            Email = "creator@wms.local"
        };
        var approver = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "approver_user",
            FullName = "Supervisor Transfer",
            Email = "approver@wms.local"
        };
        var originWh = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "WH-ORIGIN",
            Name = "Gudang Pengirim",
            IsActive = true
        };
        var originLoc = new WarehouseLocation
        {
            Id = Guid.NewGuid(),
            WarehouseId = originWh.Id,
            Code = "LOC-ORIG",
            Name = "Rak Asal",
            IsActive = true
        };
        var destWh = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "WH-DEST",
            Name = "Gudang Penerima",
            IsActive = true
        };
        var destLoc = new WarehouseLocation
        {
            Id = Guid.NewGuid(),
            WarehouseId = destWh.Id,
            Code = "LOC-DEST",
            Name = "Rak Penerima",
            IsActive = true
        };

        var category = new Category { Id = Guid.NewGuid(), Name = "Umum", IsActive = true };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Pcs", Abbreviation = "PCS", IsActive = true };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "PRD-TRF-01",
            Name = "Kabel LAN Transfer",
            CategoryId = category.Id,
            UnitId = unit.Id,
            IsActive = true
        };

        // Saldo awal di gudang asal: 20 pcs @ Rp50.000
        var originBalance = new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = originWh.Id,
            WarehouseLocationId = originLoc.Id,
            Status = StockStatus.Available,
            OnHand = 20,
            Reserved = 0
        };

        var originValuation = new InventoryValuation
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = originWh.Id,
            OnHandQty = 20,
            OnHandValue = 1000000,
            AvgCost = 50000,
            LastCost = 50000
        };

        context.Users.AddRange(creator, approver);
        context.Warehouses.AddRange(originWh, destWh);
        context.WarehouseLocations.AddRange(originLoc, destLoc);
        context.Categories.Add(category);
        context.Units.Add(unit);
        context.Products.Add(product);
        context.InventoryBalances.Add(originBalance);
        context.InventoryValuations.Add(originValuation);
        context.SaveChanges();

        return (context, originWh, originLoc, destWh, destLoc, product, creator, approver);
    }

    [Fact]
    public async Task StockTransfer_FullWorkflow_ShouldHandleSoD_InTransit_AndReceiptAtDestination()
    {
        using var context = CreateDbContext();
        var (_, originWh, originLoc, destWh, destLoc, product, creator, approver) = SeedTestData(context);
        var seqService = new DocumentSequenceService(context);
        var idempService = new IdempotencyService(context);
        var auditService = new AppAuditService(context);
        var transferService = new StockTransferService(context, seqService, idempService, auditService);

        // 1. Create Transfer Request (10 pcs)
        var createResult = await transferService.CreateDraftAsync(new CreateStockTransferCommand
        {
            OriginWarehouseId = originWh.Id,
            DestinationWarehouseId = destWh.Id,
            Reason = "Pemerataan stok cabang",
            Lines = new List<CreateStockTransferLineDto>
            {
                new()
                {
                    ProductId = product.Id,
                    OriginLocationId = originLoc.Id,
                    QuantityRequested = 10
                }
            }
        }, creator.Id);

        createResult.Succeeded.Should().BeTrue();
        var transferId = createResult.Data;

        // 2. Submit for approval
        var submitResult = await transferService.SubmitForApprovalAsync(transferId, creator.Id);
        submitResult.Succeeded.Should().BeTrue();

        // 3. Creator tries to approve -> MUST FAIL due to Separation of Duties
        var selfApproval = await transferService.ApproveAsync(transferId, creator.Id);
        selfApproval.Succeeded.Should().BeFalse();
        selfApproval.Errors.Should().Contain(e => e.Contains("Pemisahan tugas"));

        // 4. Distinct approver approves -> SUCCEEDS
        var validApproval = await transferService.ApproveAsync(transferId, approver.Id);
        validApproval.Succeeded.Should().BeTrue();

        // 5. Dispatch (Pengiriman)
        var dispatchResult = await transferService.DispatchAsync(transferId, creator.Id, Guid.NewGuid().ToString());
        dispatchResult.Succeeded.Should().BeTrue();

        // Verify status is InTransit and origin stock reduced from 20 to 10
        var transferAfterDispatch = await transferService.GetStockTransferByIdAsync(transferId);
        transferAfterDispatch.Data!.Status.Should().Be(StockTransferStatus.InTransit);
        transferAfterDispatch.Data.Lines.First().QuantityDispatched.Should().Be(10);

        var originBalanceAfter = await context.InventoryBalances
            .FirstAsync(b => b.ProductId == product.Id && b.WarehouseId == originWh.Id);
        originBalanceAfter.OnHand.Should().Be(10);

        var transferLineId = transferAfterDispatch.Data.Lines.First().Id;

        // 6. Receive at Destination (Penerimaan 10 pcs di Rak Penerima)
        var receiveResult = await transferService.ReceiveAsync(transferId, new ReceiveStockTransferCommand
        {
            Lines = new List<ReceiveStockTransferLineDto>
            {
                new()
                {
                    TransferLineId = transferLineId,
                    DestinationLocationId = destLoc.Id,
                    QuantityReceived = 10
                }
            }
        }, approver.Id, Guid.NewGuid().ToString());

        receiveResult.Succeeded.Should().BeTrue();

        // Verify status is Received
        var transferAfterReceive = await transferService.GetStockTransferByIdAsync(transferId);
        transferAfterReceive.Data!.Status.Should().Be(StockTransferStatus.Received);
        transferAfterReceive.Data.Lines.First().QuantityReceived.Should().Be(10);

        // Verify destination stock and valuation created
        var destBalance = await context.InventoryBalances
            .FirstOrDefaultAsync(b => b.ProductId == product.Id && b.WarehouseId == destWh.Id);
        destBalance.Should().NotBeNull();
        destBalance!.OnHand.Should().Be(10);

        var destValuation = await context.InventoryValuations
            .FirstOrDefaultAsync(v => v.ProductId == product.Id && v.WarehouseId == destWh.Id);
        destValuation.Should().NotBeNull();
        destValuation!.OnHandQty.Should().Be(10);
        destValuation.AvgCost.Should().Be(50000);
        destValuation.OnHandValue.Should().Be(500000);
    }
}
