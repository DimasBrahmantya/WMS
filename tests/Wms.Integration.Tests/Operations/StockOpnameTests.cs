using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Operations.Opname;
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

public class StockOpnameTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    private (WmsDbContext context, Warehouse warehouse, WarehouseLocation location, Product product, ApplicationUser creator, ApplicationUser supervisor) SeedTestData(WmsDbContext context)
    {
        var creator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "opname_user",
            FullName = "Petugas Opname",
            Email = "opname@wms.local"
        };
        var supervisor = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "supervisor_user",
            FullName = "Supervisor Gudang",
            Email = "supervisor@wms.local"
        };
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "WH-OPN-1",
            Name = "Gudang Opname",
            IsActive = true
        };
        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(),
            WarehouseId = warehouse.Id,
            Code = "LOC-OPN",
            Name = "Rak Opname",
            IsActive = true
        };
        var category = new Category { Id = Guid.NewGuid(), Name = "Umum", IsActive = true };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Pcs", Abbreviation = "PCS", IsActive = true };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "PRD-OPN-01",
            Name = "Barang Opname Test",
            CategoryId = category.Id,
            UnitId = unit.Id,
            IsActive = true
        };

        // Saldo sistem awal: 50 pcs @ Rp20.000
        var balance = new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            Status = StockStatus.Available,
            OnHand = 50,
            Reserved = 0
        };

        var valuation = new InventoryValuation
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            OnHandQty = 50,
            OnHandValue = 1000000,
            AvgCost = 20000,
            LastCost = 20000
        };

        context.Users.AddRange(creator, supervisor);
        context.Warehouses.Add(warehouse);
        context.WarehouseLocations.Add(location);
        context.Categories.Add(category);
        context.Units.Add(unit);
        context.Products.Add(product);
        context.InventoryBalances.Add(balance);
        context.InventoryValuations.Add(valuation);
        context.SaveChanges();

        return (context, warehouse, location, product, creator, supervisor);
    }

    [Fact]
    public async Task StockOpname_FullWorkflow_ShouldHandleSnapshot_Recount_SoD_AndPostingAdjustment()
    {
        using var context = CreateDbContext();
        var (_, warehouse, location, product, creator, supervisor) = SeedTestData(context);
        var seqService = new DocumentSequenceService(context);
        var idempService = new IdempotencyService(context);
        var auditService = new AppAuditService(context);
        var opnameService = new StockOpnameService(context, seqService, idempService, auditService);

        // 1. Create Opname Session
        var createResult = await opnameService.CreateSessionAsync(new CreateStockOpnameSessionCommand
        {
            WarehouseId = warehouse.Id,
            Notes = "Opname bulanan"
        }, creator.Id);

        createResult.Succeeded.Should().BeTrue();
        var sessionId = createResult.Data;

        // 2. Start Counting & Snapshot
        var startResult = await opnameService.StartCountingAsync(sessionId, creator.Id);
        startResult.Succeeded.Should().BeTrue();

        var sessionAfterStart = await opnameService.GetSessionByIdAsync(sessionId);
        sessionAfterStart.Data!.Status.Should().Be(StockOpnameStatus.Counting);
        sessionAfterStart.Data.Lines.Should().HaveCount(1);
        sessionAfterStart.Data.Lines.First().SystemSnapshotQuantity.Should().Be(50);
        sessionAfterStart.Data.Lines.First().SystemSnapshotAvgCost.Should().Be(20000);

        var lineId = sessionAfterStart.Data.Lines.First().Id;

        // 3. Record count (Counted: 48 pcs, Recount: 47 pcs -> Selisih -3 pcs)
        var recordResult = await opnameService.RecordCountsAsync(sessionId, new RecordOpnameCountCommand
        {
            Lines = new List<RecordOpnameLineCountDto>
            {
                new()
                {
                    LineId = lineId,
                    CountedQuantity = 48,
                    RecountQuantity = 47,
                    Notes = "Fisik kurang 3 unit setelah recount"
                }
            }
        }, creator.Id);

        recordResult.Succeeded.Should().BeTrue();

        var sessionAfterCount = await opnameService.GetSessionByIdAsync(sessionId);
        sessionAfterCount.Data!.Status.Should().Be(StockOpnameStatus.Review);
        sessionAfterCount.Data.TotalVarianceQuantity.Should().Be(-3);
        sessionAfterCount.Data.TotalVarianceValue.Should().Be(-60000);

        // 4. Submit for Approval
        var submitResult = await opnameService.SubmitForApprovalAsync(sessionId, creator.Id);
        submitResult.Succeeded.Should().BeTrue();

        // 5. Creator tries to approve -> FAILS due to Separation of Duties
        var selfApproval = await opnameService.ApproveAsync(sessionId, creator.Id);
        selfApproval.Succeeded.Should().BeFalse();
        selfApproval.Errors.Should().Contain(e => e.Contains("Pemisahan tugas"));

        // 6. Supervisor approves -> SUCCEEDS
        var supervisorApproval = await opnameService.ApproveAsync(sessionId, supervisor.Id);
        supervisorApproval.Succeeded.Should().BeTrue();

        // 7. Post Adjustment to Ledger
        var postResult = await opnameService.PostAdjustmentAsync(sessionId, supervisor.Id, Guid.NewGuid().ToString());
        postResult.Succeeded.Should().BeTrue();

        // 8. Verify ledger and balances
        var sessionPosted = await opnameService.GetSessionByIdAsync(sessionId);
        sessionPosted.Data!.Status.Should().Be(StockOpnameStatus.Posted);

        var balanceAfter = await context.InventoryBalances
            .FirstAsync(b => b.ProductId == product.Id && b.WarehouseId == warehouse.Id);
        balanceAfter.OnHand.Should().Be(47); // Berkurang 3 pcs sesuai hasil fisik

        var movement = await context.StockMovements
            .FirstOrDefaultAsync(m => m.SourceDocumentId == sessionId && m.MovementType == MovementType.StockAdjustment);
        movement.Should().NotBeNull();
        movement!.Quantity.Should().Be(3);
        movement.Direction.Should().Be(MovementDirection.Out);
        movement.UnitCost.Should().Be(20000);
        movement.TotalValue.Should().Be(60000);
    }
}
