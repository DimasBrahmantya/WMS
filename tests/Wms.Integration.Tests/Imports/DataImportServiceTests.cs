using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Imports;
using Wms.Domain.Entities.Master;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Imports;
using Wms.Integration.Tests.Batches;
using Wms.Integration.Tests.SalesOrders;
using Xunit;

namespace Wms.Integration.Tests.Imports;

public class DataImportServiceTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    [Fact]
    public async Task ValidateProductImportAsync_WithValidCsv_ShouldReturnValidResult()
    {
        using var context = CreateDbContext();
        var catId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = catId, Name = "Elektronik", IsActive = true });
        context.Units.Add(new Unit { Id = unitId, Name = "Pcs", Abbreviation = "PCS", IsActive = true });
        await context.SaveChangesAsync();

        var csvContent = "Sku,Name,Barcode,Brand,Category,Unit,MinStock,MaxStock,TrackingMethod\n" +
                         "SKU-IMPORT-1,Kabel USB,111222,Logitech,Elektronik,Pcs,5,50,None\n" +
                         "SKU-IMPORT-2,Mouse Wireless,111333,Logitech,Elektronik,Pcs,10,100,None";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        var clock = new FakeClock();
        var seqService = new FakeDocumentSequenceService();
        var auditService = new FakeAppAuditService();
        var service = new DataImportService(context, seqService, auditService, clock);

        var result = await service.ValidateProductImportAsync(stream, "csv");

        result.TotalRows.Should().Be(2);
        result.ValidRowsCount.Should().Be(2);
        result.ErrorRowsCount.Should().Be(0);
        result.CanCommit.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateProductImportAsync_WithDuplicateSku_ShouldReportError()
    {
        using var context = CreateDbContext();
        var catId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = catId, Name = "Elektronik", IsActive = true });
        context.Units.Add(new Unit { Id = unitId, Name = "Pcs", Abbreviation = "PCS", IsActive = true });
        // Already registered in DB
        context.Products.Add(new Product { Id = Guid.NewGuid(), Sku = "SKU-EXISTING", Name = "Sudah Ada", CategoryId = catId, UnitId = unitId, IsActive = true });
        await context.SaveChangesAsync();

        var csvContent = "Sku,Name,Barcode,Brand,Category,Unit,MinStock,MaxStock,TrackingMethod\n" +
                         "SKU-EXISTING,Kabel USB,111222,Logitech,Elektronik,Pcs,5,50,None\n" +
                         "SKU-DUPLICATE,Barang A,,Logitech,Elektronik,Pcs,5,50,None\n" +
                         "SKU-DUPLICATE,Barang B,,Logitech,Elektronik,Pcs,5,50,None";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        var clock = new FakeClock();
        var seqService = new FakeDocumentSequenceService();
        var auditService = new FakeAppAuditService();
        var service = new DataImportService(context, seqService, auditService, clock);

        var result = await service.ValidateProductImportAsync(stream, "csv");

        result.TotalRows.Should().Be(3);
        result.ErrorRowsCount.Should().BeGreaterThan(0);
        result.CanCommit.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("sudah terdaftar"));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("duplikat"));
    }

    [Fact]
    public async Task CommitImportAsync_Products_ShouldPersistSuccessfully()
    {
        using var context = CreateDbContext();
        var catId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = catId, Name = "Elektronik", IsActive = true });
        context.Units.Add(new Unit { Id = unitId, Name = "Pcs", Abbreviation = "PCS", IsActive = true });
        await context.SaveChangesAsync();

        var clock = new FakeClock();
        var seqService = new FakeDocumentSequenceService();
        var auditService = new FakeAppAuditService();
        var service = new DataImportService(context, seqService, auditService, clock);

        var cmd = new CommitImportCommand
        {
            ImportType = ImportType.Products,
            ProductRows = new List<ProductImportRowDto>
            {
                new ProductImportRowDto
                {
                    RowNumber = 2,
                    Sku = "SKU-COMMITTED",
                    Name = "Barang Baru Komitmen",
                    CategoryName = "Elektronik",
                    UnitName = "Pcs",
                    MinStock = 5,
                    IsValid = true
                }
            }
        };

        var result = await service.CommitImportAsync(cmd, Guid.NewGuid());
        result.Succeeded.Should().BeTrue();
        result.Data!.ImportedCount.Should().Be(1);

        var product = await context.Products.FirstOrDefaultAsync(p => p.Sku == "SKU-COMMITTED");
        product.Should().NotBeNull();
        product!.Name.Should().Be("Barang Baru Komitmen");
    }
}
