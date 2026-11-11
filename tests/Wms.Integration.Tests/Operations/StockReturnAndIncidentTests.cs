using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Operations.Incidents;
using Wms.Application.Features.Operations.Returns;
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

public class StockReturnAndIncidentTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    private (WmsDbContext context, Warehouse warehouse, WarehouseLocation location, Product product, ApplicationUser reporter, ApplicationUser supervisor, Supplier supplier, Customer customer) SeedTestData(WmsDbContext context)
    {
        var reporter = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "staff_user",
            FullName = "Staff Pelapor",
            Email = "staff@wms.local"
        };
        var supervisor = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "spv_user",
            FullName = "Supervisor Penyetuju",
            Email = "spv@wms.local"
        };
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "WH-RET-1",
            Name = "Gudang Retur",
            IsActive = true
        };
        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(),
            WarehouseId = warehouse.Id,
            Code = "LOC-RET",
            Name = "Rak Retur",
            IsActive = true
        };
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Code = "SUP-RET",
            Name = "Supplier Retur",
            IsActive = true
        };
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Code = "CUST-RET",
            Name = "Customer Retur",
            IsActive = true
        };
        var category = new Category { Id = Guid.NewGuid(), Name = "Umum", IsActive = true };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Pcs", Abbreviation = "PCS", IsActive = true };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "PRD-RET-01",
            Name = "Barang Retur/Incident",
            CategoryId = category.Id,
            UnitId = unit.Id,
            IsActive = true
        };

        // Saldo awal Available: 30 pcs @ Rp10.000
        var balance = new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            WarehouseLocationId = location.Id,
            Status = StockStatus.Available,
            OnHand = 30,
            Reserved = 0
        };

        var valuation = new InventoryValuation
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            OnHandQty = 30,
            OnHandValue = 300000,
            AvgCost = 10000,
            LastCost = 10000
        };

        context.Users.AddRange(reporter, supervisor);
        context.Warehouses.Add(warehouse);
        context.WarehouseLocations.Add(location);
        context.Suppliers.Add(supplier);
        context.Customers.Add(customer);
        context.Categories.Add(category);
        context.Units.Add(unit);
        context.Products.Add(product);
        context.InventoryBalances.Add(balance);
        context.InventoryValuations.Add(valuation);
        context.SaveChanges();

        return (context, warehouse, location, product, reporter, supervisor, supplier, customer);
    }

    [Fact]
    public async Task StockReturn_CustomerReturn_ToQuarantine_ShouldCreateQuarantineStock()
    {
        using var context = CreateDbContext();
        var (_, warehouse, location, product, reporter, supervisor, _, customer) = SeedTestData(context);
        var seqService = new DocumentSequenceService(context);
        var idempService = new IdempotencyService(context);
        var auditService = new AppAuditService(context);
        var returnService = new StockReturnService(context, seqService, idempService, auditService);

        // 1. Create Return from customer for 5 damaged items
        var createResult = await returnService.CreateDraftAsync(new CreateStockReturnCommand
        {
            WarehouseId = warehouse.Id,
            ReturnType = StockReturnType.ReturnFromCustomer,
            CustomerId = customer.Id,
            Reason = "Barang cacat pabrik",
            Lines = new List<CreateStockReturnLineDto>
            {
                new()
                {
                    ProductId = product.Id,
                    LocationId = location.Id,
                    Quantity = 5,
                    UnitPrice = 10000,
                    TargetStockStatus = StockStatus.Quarantine,
                    Notes = "Masuk karantina"
                }
            }
        }, reporter.Id);

        createResult.Succeeded.Should().BeTrue();
        var returnId = createResult.Data;

        // 2. Submit & Approve (SoD check)
        await returnService.SubmitForApprovalAsync(returnId, reporter.Id);

        var selfApprove = await returnService.ApproveAsync(returnId, reporter.Id);
        selfApprove.Succeeded.Should().BeFalse();
        selfApprove.Errors.Should().Contain(e => e.Contains("Pemisahan tugas"));

        var spvApprove = await returnService.ApproveAsync(returnId, supervisor.Id);
        spvApprove.Succeeded.Should().BeTrue();

        // 3. Post Return
        var postResult = await returnService.PostAsync(returnId, supervisor.Id, Guid.NewGuid().ToString());
        postResult.Succeeded.Should().BeTrue();

        // 4. Verify stock
        var quarantineBalance = await context.InventoryBalances
            .FirstOrDefaultAsync(b => b.ProductId == product.Id && b.WarehouseId == warehouse.Id && b.Status == StockStatus.Quarantine);

        quarantineBalance.Should().NotBeNull();
        quarantineBalance!.OnHand.Should().Be(5);

        // Saldo Available awal (30) tetap 30
        var availableBalance = await context.InventoryBalances
            .FirstAsync(b => b.ProductId == product.Id && b.WarehouseId == warehouse.Id && b.Status == StockStatus.Available);
        availableBalance.OnHand.Should().Be(30);
    }

    [Fact]
    public async Task StockIncident_LostAndDamaged_ShouldEnforceSoD_AndModifyInventoryOnlyOnResolve()
    {
        using var context = CreateDbContext();
        var (_, warehouse, location, product, reporter, supervisor, _, _) = SeedTestData(context);
        var seqService = new DocumentSequenceService(context);
        var idempService = new IdempotencyService(context);
        var auditService = new AppAuditService(context);
        var incidentService = new StockIncidentService(context, seqService, idempService, auditService);

        // 1. Report Lost Incident (2 pcs)
        var reportResult = await incidentService.ReportIncidentAsync(new CreateStockIncidentCommand
        {
            WarehouseId = warehouse.Id,
            ProductId = product.Id,
            LocationId = location.Id,
            Category = StockIncidentCategory.Lost,
            Quantity = 2,
            Reason = "Hilang saat penghitungan rutin"
        }, reporter.Id);

        reportResult.Succeeded.Should().BeTrue();
        var incidentId = reportResult.Data;

        // Verify balance UNCHANGED before approval/resolve (still 30)
        var balanceBefore = await context.InventoryBalances
            .FirstAsync(b => b.ProductId == product.Id && b.WarehouseId == warehouse.Id && b.Status == StockStatus.Available);
        balanceBefore.OnHand.Should().Be(30);

        // 2. Update investigation
        await incidentService.UpdateInvestigationAsync(incidentId, "Pemeriksaan CCTV tidak menemukan barang", supervisor.Id);

        // 3. Reporter cannot approve own incident
        var selfApprove = await incidentService.ApproveAsync(incidentId, reporter.Id);
        selfApprove.Succeeded.Should().BeFalse();
        selfApprove.Errors.Should().Contain(e => e.Contains("Pemisahan tugas"));

        // 4. Supervisor approves
        var spvApprove = await incidentService.ApproveAsync(incidentId, supervisor.Id);
        spvApprove.Succeeded.Should().BeTrue();

        // 5. Resolve lost incident -> cuts stock from 30 to 28
        var resolveResult = await incidentService.ResolveAsync(incidentId, "Penghapusan stok hilang disetujui", supervisor.Id, Guid.NewGuid().ToString());
        resolveResult.Succeeded.Should().BeTrue();

        var balanceAfter = await context.InventoryBalances
            .FirstAsync(b => b.ProductId == product.Id && b.WarehouseId == warehouse.Id && b.Status == StockStatus.Available);
        balanceAfter.OnHand.Should().Be(28);

        var incRecord = await incidentService.GetIncidentByIdAsync(incidentId);
        incRecord.Data!.Status.Should().Be(StockIncidentStatus.Resolved);
    }
}

