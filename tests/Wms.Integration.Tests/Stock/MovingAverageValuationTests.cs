using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Application.Features.Stock.Idempotency;
using Wms.Application.Features.Stock.Postings;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Stock;
using Xunit;

namespace Wms.Integration.Tests.Stock;

public class MovingAverageValuationTests
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
            UserName = "petugas",
            FullName = "Petugas Gudang",
            Email = "petugas@wms.local"
        };
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "GUD-01",
            Name = "Gudang Utama",
            IsActive = true
        };
        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(),
            WarehouseId = warehouse.Id,
            Code = "A-01",
            Name = "Rak A-01",
            IsActive = true
        };
        var category = new Category { Id = Guid.NewGuid(), Name = "Umum", IsActive = true };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Pcs", Abbreviation = "PCS", IsActive = true };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "BRG-001",
            Name = "Barang Uji Valuasi",
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
    public async Task TwoPurchasesWithDifferentPrices_ShouldCalculateMovingAverageCorrectly()
    {
        // Skenario Bagian 9.3:
        // Pembelian 1: 10 pcs @ Rp10.000 => total Rp100.000, avg = Rp10.000
        // Pembelian 2: 10 pcs @ Rp20.000 => total Rp200.000, cumulative: 20 pcs, total Rp300.000, avg = Rp15.000
        using var context = CreateDbContext();
        var (_, warehouse, location, product, user) = SeedTestData(context);

        var sequenceService = new DocumentSequenceService(context);
        var idempotencyService = new IdempotencyService(context);
        var postingService = new StockPostingService(context, sequenceService, idempotencyService);

        // Penerimaan 1
        var receipt1 = new GoodsReceipt
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
                    Quantity = 10m,
                    UnitCost = 10000m,
                    TotalCost = 100000m
                }
            }
        };
        context.GoodsReceipts.Add(receipt1);
        await context.SaveChangesAsync();

        var post1 = await postingService.PostGoodsReceiptAsync(receipt1.Id, user.Id, Guid.NewGuid().ToString());
        post1.Succeeded.Should().BeTrue();

        var val1 = await context.InventoryValuations.FirstOrDefaultAsync(v => v.ProductId == product.Id && v.WarehouseId == warehouse.Id);
        val1.Should().NotBeNull();
        val1!.OnHandQty.Should().Be(10m);
        val1.OnHandValue.Should().Be(100000m);
        val1.AvgCost.Should().Be(10000m);

        // Penerimaan 2
        var receipt2 = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = "GR-202611-000002",
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
                    Quantity = 10m,
                    UnitCost = 20000m,
                    TotalCost = 200000m
                }
            }
        };
        context.GoodsReceipts.Add(receipt2);
        await context.SaveChangesAsync();

        var post2 = await postingService.PostGoodsReceiptAsync(receipt2.Id, user.Id, Guid.NewGuid().ToString());
        post2.Succeeded.Should().BeTrue();

        var val2 = await context.InventoryValuations.FirstOrDefaultAsync(v => v.ProductId == product.Id && v.WarehouseId == warehouse.Id);
        val2.Should().NotBeNull();
        val2!.OnHandQty.Should().Be(20m);
        val2.OnHandValue.Should().Be(300000m);
        val2.AvgCost.Should().Be(15000m);

        // Saldo fisik
        var balance = await context.InventoryBalances.FirstOrDefaultAsync(b => b.ProductId == product.Id && b.WarehouseLocationId == location.Id);
        balance.Should().NotBeNull();
        balance!.OnHand.Should().Be(20m);
    }

    [Fact]
    public async Task GoodsIssue_ShouldConsumeAtMovingAverageAndKeepAvgCostConstant()
    {
        // Lanjutan: stok 20 pcs @ Rp15.000
        // Pengeluaran 5 pcs: dinilai pada Rp15.000.
        // Sisa stok 15 pcs, total nilai Rp225.000, avg_cost tetap Rp15.000
        using var context = CreateDbContext();
        var (_, warehouse, location, product, user) = SeedTestData(context);

        var sequenceService = new DocumentSequenceService(context);
        var idempotencyService = new IdempotencyService(context);
        var postingService = new StockPostingService(context, sequenceService, idempotencyService);

        context.InventoryBalances.Add(new Domain.Entities.Inventory.InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            Status = StockStatus.Available,
            OnHand = 20m,
            Reserved = 0m
        });
        context.InventoryValuations.Add(new Domain.Entities.Inventory.InventoryValuation
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            OnHandQty = 20m,
            OnHandValue = 300000m,
            AvgCost = 15000m
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
                    Quantity = 5m
                }
            }
        };
        context.StockOuts.Add(stockOut);
        await context.SaveChangesAsync();

        var postOut = await postingService.PostStockOutAsync(stockOut.Id, user.Id, Guid.NewGuid().ToString());
        postOut.Succeeded.Should().BeTrue();

        var val = await context.InventoryValuations.FirstOrDefaultAsync(v => v.ProductId == product.Id && v.WarehouseId == warehouse.Id);
        val.Should().NotBeNull();
        val!.OnHandQty.Should().Be(15m);
        val.OnHandValue.Should().Be(225000m);
        val.AvgCost.Should().Be(15000m); // avg cost tidak berubah pada barang keluar

        var balance = await context.InventoryBalances.FirstOrDefaultAsync(b => b.ProductId == product.Id && b.WarehouseLocationId == location.Id);
        balance!.OnHand.Should().Be(15m);
    }

    [Fact]
    public async Task ZeroStock_ShouldEnsureOnHandValueIsExactlyZero()
    {
        // Bagian 9.3: "Saat on_hand_qty menjadi 0, on_hand_value harus 0 (selisih pembulatan dikoreksi)"
        using var context = CreateDbContext();
        var (_, warehouse, location, product, user) = SeedTestData(context);

        var sequenceService = new DocumentSequenceService(context);
        var idempotencyService = new IdempotencyService(context);
        var postingService = new StockPostingService(context, sequenceService, idempotencyService);

        context.InventoryBalances.Add(new Domain.Entities.Inventory.InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            Status = StockStatus.Available,
            OnHand = 3m,
            Reserved = 0m
        });
        context.InventoryValuations.Add(new Domain.Entities.Inventory.InventoryValuation
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            OnHandQty = 3m,
            OnHandValue = 10000.0001m,
            AvgCost = 3333.3333m
        });
        await context.SaveChangesAsync();

        var stockOut = new StockOut
        {
            Id = Guid.NewGuid(),
            IssueNumber = "GI-202611-000002",
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
                    Quantity = 3m
                }
            }
        };
        context.StockOuts.Add(stockOut);
        await context.SaveChangesAsync();

        var postResult = await postingService.PostStockOutAsync(stockOut.Id, user.Id, Guid.NewGuid().ToString());
        postResult.Succeeded.Should().BeTrue();

        var val = await context.InventoryValuations.FirstOrDefaultAsync(v => v.ProductId == product.Id && v.WarehouseId == warehouse.Id);
        val.Should().NotBeNull();
        val!.OnHandQty.Should().Be(0m);
        val.OnHandValue.Should().Be(0m); // Nilai harus tepat 0
    }

    [Fact]
    public async Task GoodsReceiptReversal_ShouldRecalculateMovingAverageAndPreventNegativeStock()
    {
        // Bagian 9.3: Pembatalan penerimaan barang masuk
        using var context = CreateDbContext();
        var (_, warehouse, location, product, user) = SeedTestData(context);

        var sequenceService = new DocumentSequenceService(context);
        var idempotencyService = new IdempotencyService(context);
        var postingService = new StockPostingService(context, sequenceService, idempotencyService);

        var receipt = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = "GR-202611-000003",
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
                    Quantity = 10m,
                    UnitCost = 12000m,
                    TotalCost = 120000m
                }
            }
        };
        context.GoodsReceipts.Add(receipt);
        await context.SaveChangesAsync();

        await postingService.PostGoodsReceiptAsync(receipt.Id, user.Id, Guid.NewGuid().ToString());

        // Lakukan pembatalan (Reversal)
        var revResult = await postingService.ReverseGoodsReceiptAsync(receipt.Id, user.Id, "Salah penerimaan barang", Guid.NewGuid().ToString());
        revResult.Succeeded.Should().BeTrue();

        var balance = await context.InventoryBalances.FirstOrDefaultAsync(b => b.ProductId == product.Id && b.WarehouseLocationId == location.Id);
        balance!.OnHand.Should().Be(0m);

        var val = await context.InventoryValuations.FirstOrDefaultAsync(v => v.ProductId == product.Id && v.WarehouseId == warehouse.Id);
        val!.OnHandQty.Should().Be(0m);
        val.OnHandValue.Should().Be(0m);

        // Dokumen berubah status menjadi Reversed
        var updatedReceipt = await context.GoodsReceipts.FindAsync(receipt.Id);
        updatedReceipt!.Status.Should().Be(DocumentStatus.Reversed);
        updatedReceipt.ReversalReason.Should().Be("Salah penerimaan barang");

        // Movement bertaut lawan dicatat
        var movements = await context.StockMovements.Where(m => m.SourceDocumentId == receipt.Id).ToListAsync();
        movements.Should().HaveCount(2); // 1 In, 1 Out (Reversal)
        var origMov = movements.First(m => m.Direction == MovementDirection.In);
        var revMov = movements.First(m => m.Direction == MovementDirection.Out);
        origMov.Status.Should().Be(MovementStatus.Reversed);
        origMov.ReversedByMovementId.Should().Be(revMov.Id);
        revMov.MovementType.Should().Be(MovementType.GoodsReceiptReversal);
    }
}

