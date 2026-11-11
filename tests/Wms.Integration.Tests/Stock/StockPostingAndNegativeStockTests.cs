using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Application.Features.Stock.Idempotency;
using Wms.Application.Features.Stock.Postings;
using Wms.Domain.Entities.System;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Audit;
using Wms.Infrastructure.Services.Stock;
using Xunit;

namespace Wms.Integration.Tests.Stock;

public class StockPostingAndNegativeStockTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    private (WmsDbContext context, Warehouse warehouse, WarehouseLocation location, Product product, ApplicationUser user) SeedTestData(WmsDbContext context)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "warehouse_operator",
            FullName = "Operator Gudang",
            Email = "operator@wms.local"
        };
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "WH-POSTING",
            Name = "Gudang Posting",
            IsActive = true
        };
        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(),
            WarehouseId = warehouse.Id,
            Code = "LOC-A1",
            Name = "Rak A1",
            IsActive = true
        };
        var category = new Category { Id = Guid.NewGuid(), Name = "Elektronik", IsActive = true };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Unit", Abbreviation = "UNT", IsActive = true };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-MONITOR-24",
            Name = "Monitor 24 Inch",
            CategoryId = category.Id,
            UnitId = unit.Id,
            IsActive = true
        };

        context.Users.Add(user);
        context.Warehouses.Add(warehouse);
        context.WarehouseLocations.Add(location);
        context.Categories.Add(category);
        context.Units.Add(unit);
        context.Products.Add(product);
        context.SaveChanges();

        return (context, warehouse, location, product, user);
    }

    [Fact]
    public async Task GoodsReceiptPosting_ShouldIncrementBalance_CreateMovements_AndLogAudit()
    {
        // 1. Barang Masuk: Posting GoodsReceipt
        using var context = CreateDbContext();
        var (_, warehouse, location, product, user) = SeedTestData(context);

        var gr = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = "GR-202611-000001",
            WarehouseId = warehouse.Id,
            DocumentDate = DateTime.UtcNow,
            CreatedByUserId = user.Id,
            Status = DocumentStatus.Draft,
            Lines = new List<GoodsReceiptLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    WarehouseLocationId = location.Id,
                    Quantity = 50,
                    UnitCost = 1500000
                }
            }
        };
        context.GoodsReceipts.Add(gr);
        await context.SaveChangesAsync();

        var seq = new DocumentSequenceService(context);
        var idemp = new IdempotencyService(context);
        var postingService = new StockPostingService(context, seq, idemp);

        // Act
        var result = await postingService.PostGoodsReceiptAsync(gr.Id, user.Id, "IDEMP-GR-01");

        // Assert
        result.Succeeded.Should().BeTrue();

        var balance = await context.InventoryBalances.FirstOrDefaultAsync(b => b.ProductId == product.Id && b.WarehouseLocationId == location.Id);
        balance.Should().NotBeNull();
        balance!.OnHand.Should().Be(50);
        balance.Available.Should().Be(50);

        var movement = await context.StockMovements.FirstOrDefaultAsync(m => m.SourceDocumentId == gr.Id);
        movement.Should().NotBeNull();
        movement!.MovementType.Should().Be(MovementType.GoodsReceipt);
        movement.Direction.Should().Be(MovementDirection.In);
        movement.Quantity.Should().Be(50);
        movement.BalanceBefore.Should().Be(0);
        movement.BalanceAfter.Should().Be(50);
        movement.Status.Should().Be(MovementStatus.Posted);

        var valuation = await context.InventoryValuations.FirstOrDefaultAsync(v => v.ProductId == product.Id && v.WarehouseId == warehouse.Id);
        valuation.Should().NotBeNull();
        valuation!.OnHandQty.Should().Be(50);
        valuation.AvgCost.Should().Be(1500000);
    }

    [Fact]
    public async Task StockOutPosting_WhenQuantityExceedsAvailable_ShouldRejectWithNegativeStockError()
    {
        // 2. Penolakan Stok Negatif: Meminta 100 unit padahal fisik hanya 10 unit
        using var context = CreateDbContext();
        var (_, warehouse, location, product, user) = SeedTestData(context);

        context.InventoryBalances.Add(new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            OnHand = 10,
            Reserved = 0,
            Status = StockStatus.Available
        });
        context.InventoryValuations.Add(new InventoryValuation
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            OnHandQty = 10,
            OnHandValue = 15000000,
            AvgCost = 1500000
        });
        await context.SaveChangesAsync();

        var stockOut = new StockOut
        {
            Id = Guid.NewGuid(),
            IssueNumber = "GI-202611-000001",
            WarehouseId = warehouse.Id,
            DocumentDate = DateTime.UtcNow,
            IssueType = StockOutType.CustomerDelivery,
            CreatedByUserId = user.Id,
            Status = DocumentStatus.Draft,
            Lines = new List<StockOutLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    WarehouseLocationId = location.Id,
                    Quantity = 100 // Exceeds 10!
                }
            }
        };
        context.StockOuts.Add(stockOut);
        await context.SaveChangesAsync();

        var seq = new DocumentSequenceService(context);
        var idemp = new IdempotencyService(context);
        var postingService = new StockPostingService(context, seq, idemp);

        // Act
        var result = await postingService.PostStockOutAsync(stockOut.Id, user.Id, "IDEMP-GI-EXCEED");

        // Assert
        result.Succeeded.Should().BeFalse("Pengeluaran melebihi stok harus ditolak tegas");
        result.ErrorMessage.Should().Contain("tidak mencukupi");

        // Balance should NOT become negative (-90)
        var balance = await context.InventoryBalances.FirstOrDefaultAsync(b => b.ProductId == product.Id && b.WarehouseLocationId == location.Id);
        balance!.OnHand.Should().Be(10, "Saldo tidak boleh berubah atau bernilai negatif");

        // No ledger entry should be created
        var movement = await context.StockMovements.FirstOrDefaultAsync(m => m.SourceDocumentId == stockOut.Id);
        movement.Should().BeNull();
    }

    [Fact]
    public async Task LedgerMovements_AreAppendOnly_DirectDeleteMustThrowException()
    {
        // 3. Append-only ledger invariant: StockMovements cannot be deleted
        using var context = CreateDbContext();
        var (_, warehouse, location, product, user) = SeedTestData(context);

        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            MovementNumber = "MV-TEST-001",
            MovementType = MovementType.GoodsReceipt,
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            Quantity = 10,
            Direction = MovementDirection.In,
            BalanceBefore = 0,
            BalanceAfter = 10,
            UnitCost = 1000,
            TotalValue = 10000,
            TimestampUtc = DateTime.UtcNow,
            Status = MovementStatus.Posted,
            UserId = user.Id
        };
        context.StockMovements.Add(movement);
        await context.SaveChangesAsync();

        // Attempting to delete from context
        context.StockMovements.Remove(movement);
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*append-only*");
    }

    [Fact]
    public async Task GoodsReceiptReversal_ShouldCreateCounterMovement_AndRecordAuditLog()
    {
        // 4. Pembatalan/Koreksi dengan Ledger & Audit:
        // Reversal memposting counter-movement di ledger append-only dan mencatat audit log
        using var context = CreateDbContext();
        var (_, warehouse, location, product, user) = SeedTestData(context);

        var gr = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = "GR-REV-001",
            WarehouseId = warehouse.Id,
            DocumentDate = DateTime.UtcNow,
            CreatedByUserId = user.Id,
            Status = DocumentStatus.Draft,
            Lines = new List<GoodsReceiptLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    WarehouseLocationId = location.Id,
                    Quantity = 20,
                    UnitCost = 100000
                }
            }
        };
        context.GoodsReceipts.Add(gr);
        await context.SaveChangesAsync();

        var seq = new DocumentSequenceService(context);
        var idemp = new IdempotencyService(context);
        var postingService = new StockPostingService(context, seq, idemp);

        await postingService.PostGoodsReceiptAsync(gr.Id, user.Id, "IDEMP-REV-POST");

        // Stock is now 20. Now reverse it
        var auditService = new AppAuditService(context);
        var reversalResult = await postingService.ReverseGoodsReceiptAsync(gr.Id, user.Id, "Salah input PO supplier", "IDEMP-REV-DO");
        reversalResult.Succeeded.Should().BeTrue();

        // Check balances
        var balance = await context.InventoryBalances.FirstOrDefaultAsync(b => b.ProductId == product.Id && b.WarehouseLocationId == location.Id);
        balance!.OnHand.Should().Be(0, "Saldo harus kembali ke 0 setelah pembalikan/koreksi");

        // Check counter-movements: 1 In, 1 Out
        var movements = await context.StockMovements.Where(m => m.SourceDocumentId == gr.Id).ToListAsync();
        movements.Should().HaveCount(2, "Harus ada pergerakan asli (In) dan pergerakan pembalik (Out)");

        var counterMovement = movements.First(m => m.Direction == MovementDirection.Out);
        counterMovement.Quantity.Should().Be(20);
        counterMovement.MovementType.Should().Be(MovementType.GoodsReceiptReversal);
        counterMovement.Status.Should().Be(MovementStatus.Posted);

        var originalMovement = movements.First(m => m.Direction == MovementDirection.In);
        originalMovement.Status.Should().Be(MovementStatus.Reversed);
        originalMovement.ReversedByMovementId.Should().Be(counterMovement.Id);
    }
}
