using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Audit;
using Wms.Application.Features.Operations.PurchaseOrders;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Application.Features.Stock.Idempotency;
using Wms.Application.Features.Stock.Postings;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Audit;
using Wms.Infrastructure.Services.Operations;
using Wms.Infrastructure.Services.Stock;
using Xunit;

namespace Wms.Integration.Tests.Operations;

public class PurchaseOrderTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    private (WmsDbContext context, Warehouse warehouse, WarehouseLocation location, Supplier supplier, Product product, ApplicationUser creator, ApplicationUser approver) SeedTestData(WmsDbContext context)
    {
        var creator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "creator_user",
            FullName = "Admin Gudang Pembuat",
            Email = "creator@wms.local"
        };
        var approver = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "approver_user",
            FullName = "Manager Approver",
            Email = "approver@wms.local"
        };
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "GUD-PO-1",
            Name = "Gudang Pembelian",
            IsActive = true
        };
        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(),
            WarehouseId = warehouse.Id,
            Code = "LOC-01",
            Name = "Rak Penerimaan",
            IsActive = true
        };
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Code = "SUPP-01",
            Name = "PT Supplier Sukses",
            IsActive = true
        };
        var category = new Category { Id = Guid.NewGuid(), Name = "Elektronik", IsActive = true };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Unit", Abbreviation = "UNT", IsActive = true };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "PRD-PO-01",
            Name = "Laptop Uji PO",
            CategoryId = category.Id,
            UnitId = unit.Id,
            IsActive = true
        };

        context.Users.AddRange(creator, approver);
        context.Warehouses.Add(warehouse);
        context.WarehouseLocations.Add(location);
        context.Suppliers.Add(supplier);
        context.Categories.Add(category);
        context.Units.Add(unit);
        context.Products.Add(product);
        context.SaveChanges();

        return (context, warehouse, location, supplier, product, creator, approver);
    }

    [Fact]
    public async Task PurchaseOrder_SeparationOfDuties_ShouldRejectCreatorApproval_AndAllowDistinctApprover()
    {
        using var context = CreateDbContext();
        var (_, warehouse, _, supplier, product, creator, approver) = SeedTestData(context);
        var seqService = new DocumentSequenceService(context);
        var auditService = new AppAuditService(context);
        var poService = new PurchaseOrderService(context, seqService, auditService);

        // 1. Create PO
        var createCommand = new CreatePurchaseOrderCommand
        {
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            Notes = "Pesanan pengadaan",
            Lines = new List<PurchaseOrderLineItemCommand>
            {
                new() { ProductId = product.Id, OrderedQuantity = 10, UnitPrice = 5000000 }
            }
        };

        var createResult = await poService.CreatePurchaseOrderAsync(createCommand, creator.Id);
        createResult.Succeeded.Should().BeTrue();
        var poId = createResult.Data;

        // 2. Submit for approval
        var submitResult = await poService.SubmitForApprovalAsync(poId, creator.Id);
        submitResult.Succeeded.Should().BeTrue();

        // 3. Creator tries to approve own PO -> MUST FAIL due to Separation of Duties
        var selfApprovalResult = await poService.ApprovePurchaseOrderAsync(poId, creator.Id);
        selfApprovalResult.Succeeded.Should().BeFalse();
        selfApprovalResult.Errors.Should().Contain(e => e.Contains("Pemisahan tugas"));

        // 4. Distinct approver approves -> MUST SUCCEED
        var validApprovalResult = await poService.ApprovePurchaseOrderAsync(poId, approver.Id);
        validApprovalResult.Succeeded.Should().BeTrue();

        var po = await poService.GetPurchaseOrderByIdAsync(poId);
        po.Data.Status.Should().Be(PurchaseOrderStatus.Approved);
        po.Data.ApprovedByUserId.Should().Be(approver.Id);
    }

    [Fact]
    public async Task PurchaseOrder_PartialAndFullReceipt_ShouldUpdateStatusAndQuantitiesCorrectly()
    {
        using var context = CreateDbContext();
        var (_, warehouse, location, supplier, product, creator, approver) = SeedTestData(context);
        var seqService = new DocumentSequenceService(context);
        var auditService = new AppAuditService(context);
        var poService = new PurchaseOrderService(context, seqService, auditService);
        var idempService = new IdempotencyService(context);
        var postingService = new StockPostingService(context, seqService, idempService);

        // 1. Create & Approve PO
        var createResult = await poService.CreatePurchaseOrderAsync(new CreatePurchaseOrderCommand
        {
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            Lines = new List<PurchaseOrderLineItemCommand>
            {
                new() { ProductId = product.Id, OrderedQuantity = 10, UnitPrice = 100000 }
            }
        }, creator.Id);

        var poId = createResult.Data;
        await poService.SubmitForApprovalAsync(poId, creator.Id);
        await poService.ApprovePurchaseOrderAsync(poId, approver.Id);
        await poService.MarkAsOrderedAsync(poId, creator.Id);

        var poBefore = await poService.GetPurchaseOrderByIdAsync(poId);
        poBefore.Data.Status.Should().Be(PurchaseOrderStatus.Ordered);
        var poLineId = poBefore.Data.Lines.First().Id;

        // 2. First partial receipt (4 units)
        var grNumber1 = await seqService.GetNextDocumentNumberAsync("GR");
        var gr1 = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = grNumber1,
            DocumentDate = DateTime.UtcNow,
            WarehouseId = warehouse.Id,
            SupplierId = supplier.Id,
            PurchaseOrderId = poId,
            PurchaseOrderNumber = poBefore.Data.PoNumber,
            Status = DocumentStatus.Draft,
            CreatedByUserId = creator.Id,
            Lines = new List<GoodsReceiptLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    PurchaseOrderLineId = poLineId,
                    WarehouseLocationId = location.Id,
                    Quantity = 4,
                    UnitCost = 100000,
                    TotalCost = 400000
                }
            }
        };
        context.GoodsReceipts.Add(gr1);
        await context.SaveChangesAsync();

        var postResult1 = await postingService.PostGoodsReceiptAsync(gr1.Id, creator.Id, Guid.NewGuid().ToString());
        postResult1.Succeeded.Should().BeTrue();

        // Verify PO status is PartiallyReceived
        var poAfterGr1 = await poService.GetPurchaseOrderByIdAsync(poId);
        poAfterGr1.Data.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        poAfterGr1.Data.Lines.First().ReceivedQuantity.Should().Be(4);
        poAfterGr1.Data.Lines.First().RemainingQuantity.Should().Be(6);

        // 3. Second receipt completing remaining 6 units
        var grNumber2 = await seqService.GetNextDocumentNumberAsync("GR");
        var gr2 = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = grNumber2,
            DocumentDate = DateTime.UtcNow,
            WarehouseId = warehouse.Id,
            SupplierId = supplier.Id,
            PurchaseOrderId = poId,
            PurchaseOrderNumber = poBefore.Data.PoNumber,
            Status = DocumentStatus.Draft,
            CreatedByUserId = creator.Id,
            Lines = new List<GoodsReceiptLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    PurchaseOrderLineId = poLineId,
                    WarehouseLocationId = location.Id,
                    Quantity = 6,
                    UnitCost = 100000,
                    TotalCost = 600000
                }
            }
        };
        context.GoodsReceipts.Add(gr2);
        await context.SaveChangesAsync();

        var postResult2 = await postingService.PostGoodsReceiptAsync(gr2.Id, creator.Id, Guid.NewGuid().ToString());
        postResult2.Succeeded.Should().BeTrue();

        // Verify PO status is now Received (fulfilled)
        var poAfterGr2 = await poService.GetPurchaseOrderByIdAsync(poId);
        poAfterGr2.Data.Status.Should().Be(PurchaseOrderStatus.Received);
        poAfterGr2.Data.Lines.First().ReceivedQuantity.Should().Be(10);
        poAfterGr2.Data.Lines.First().RemainingQuantity.Should().Be(0);

        // 4. Reversal of GR2 rolls back the PO status to PartiallyReceived
        var revResult = await postingService.ReverseGoodsReceiptAsync(gr2.Id, approver.Id, "Pembatalan parsial", Guid.NewGuid().ToString());
        revResult.Succeeded.Should().BeTrue();

        var poAfterRev = await poService.GetPurchaseOrderByIdAsync(poId);
        poAfterRev.Data.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        poAfterRev.Data.Lines.First().ReceivedQuantity.Should().Be(4);
        poAfterRev.Data.Lines.First().RemainingQuantity.Should().Be(6);
    }
}
