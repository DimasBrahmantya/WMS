using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Master.Categories;
using Wms.Application.Features.Master.Products;
using Wms.Application.Features.Master.Units;
using Wms.Application.Features.Master.Warehouses;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Xunit;

namespace Wms.Integration.Tests;

public class MasterServiceTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    [Fact]
    public async Task CategoryService_Create_ShouldPreventDuplicateName()
    {
        using var context = CreateDbContext();
        var service = new CategoryService(context, new CreateCategoryValidator(), new UpdateCategoryValidator());

        var first = await service.CreateCategoryAsync(new CreateCategoryCommand("Elektronik", "Deskripsi 1"));
        first.Succeeded.Should().BeTrue();

        var duplicate = await service.CreateCategoryAsync(new CreateCategoryCommand("elektronik", "Deskripsi 2"));
        duplicate.Succeeded.Should().BeFalse();
        duplicate.ErrorMessage.Should().Contain("sudah ada");
    }

    [Fact]
    public async Task CategoryService_Delete_ShouldFail_WhenCategoryHasProducts()
    {
        using var context = CreateDbContext();
        var service = new CategoryService(context, new CreateCategoryValidator(), new UpdateCategoryValidator());

        var cat = new Category { Id = Guid.NewGuid(), Name = "Elektronik", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Pcs", Abbreviation = "PCS", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "ELK-001",
            Name = "Mouse",
            CategoryId = cat.Id,
            UnitId = unit.Id,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Categories.Add(cat);
        context.Units.Add(unit);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var deleteResult = await service.DeleteCategoryAsync(cat.Id);
        deleteResult.Succeeded.Should().BeFalse();
        deleteResult.ErrorMessage.Should().Contain("masih digunakan");
    }

    [Fact]
    public async Task CategoryService_ToggleStatus_ShouldFlipIsActive()
    {
        using var context = CreateDbContext();
        var service = new CategoryService(context, new CreateCategoryValidator(), new UpdateCategoryValidator());

        var cat = new Category { Id = Guid.NewGuid(), Name = "Pakaian", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var toggleResult = await service.ToggleCategoryStatusAsync(cat.Id);
        toggleResult.Succeeded.Should().BeTrue();

        var updated = await context.Categories.FindAsync(cat.Id);
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UnitService_Create_ShouldPreventDuplicateAbbreviation()
    {
        using var context = CreateDbContext();
        var service = new UnitService(context, new CreateUnitValidator(), new UpdateUnitValidator());

        var first = await service.CreateUnitAsync(new CreateUnitCommand("Pieces", "PCS"));
        first.Succeeded.Should().BeTrue();

        var duplicate = await service.CreateUnitAsync(new CreateUnitCommand("Pieces Lain", "pcs"));
        duplicate.Succeeded.Should().BeFalse();
        duplicate.ErrorMessage.Should().Contain("sudah ada");
    }

    [Fact]
    public async Task WarehouseService_Create_ShouldPreventDuplicateCode()
    {
        using var context = CreateDbContext();
        var service = new WarehouseService(
            context,
            new CreateWarehouseValidator(),
            new UpdateWarehouseValidator(),
            new CreateWarehouseLocationValidator(),
            new UpdateWarehouseLocationValidator());

        var first = await service.CreateWarehouseAsync(new CreateWarehouseCommand("GDG-JKT", "Gudang Jakarta", "Alamat", "Budi"));
        first.Succeeded.Should().BeTrue();

        var duplicate = await service.CreateWarehouseAsync(new CreateWarehouseCommand("gdg-jkt", "Gudang Jakarta 2", null, null));
        duplicate.Succeeded.Should().BeFalse();
        duplicate.ErrorMessage.Should().Contain("sudah ada");
    }

    [Fact]
    public async Task WarehouseService_Delete_ShouldFail_WhenWarehouseHasLocations()
    {
        using var context = CreateDbContext();
        var service = new WarehouseService(
            context,
            new CreateWarehouseValidator(),
            new UpdateWarehouseValidator(),
            new CreateWarehouseLocationValidator(),
            new UpdateWarehouseLocationValidator());

        var wh = new Warehouse { Id = Guid.NewGuid(), Code = "GDG-1", Name = "Gudang 1", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        var loc = new WarehouseLocation { Id = Guid.NewGuid(), WarehouseId = wh.Id, Code = "LOC-1", Name = "Rak 1", IsActive = true, CreatedAtUtc = DateTime.UtcNow };

        context.Warehouses.Add(wh);
        context.WarehouseLocations.Add(loc);
        await context.SaveChangesAsync();

        var deleteResult = await service.DeleteWarehouseAsync(wh.Id);
        deleteResult.Succeeded.Should().BeFalse();
        deleteResult.ErrorMessage.Should().Contain("masih memiliki lokasi rak");
    }

    [Fact]
    public async Task ProductService_Create_ShouldPreventDuplicateSkuCaseInsensitive()
    {
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "Kategori A", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Pcs", Abbreviation = "PCS", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        context.Categories.Add(cat);
        context.Units.Add(unit);
        await context.SaveChangesAsync();

        var service = new ProductService(context, new CreateProductValidator(), new UpdateProductValidator());

        var first = await service.CreateProductAsync(new CreateProductCommand(
            "SKU-ABC", null, "Barang 1", null, null, cat.Id, unit.Id, 0, 100, TrackingMethod.None));
        first.Succeeded.Should().BeTrue();

        var duplicate = await service.CreateProductAsync(new CreateProductCommand(
            "sku-abc", null, "Barang 2", null, null, cat.Id, unit.Id, 0, 100, TrackingMethod.None));
        duplicate.Succeeded.Should().BeFalse();
        duplicate.ErrorMessage.Should().Contain("sudah ada");
    }

    [Fact]
    public async Task ProductService_CreateAndUpdate_ShouldPreventDuplicateBarcode()
    {
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "Kategori Barcode", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Pcs", Abbreviation = "PCS", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        context.Categories.Add(cat);
        context.Units.Add(unit);
        await context.SaveChangesAsync();

        var service = new ProductService(context, new CreateProductValidator(), new UpdateProductValidator());

        // 1. Create first product with barcode "8991234567890"
        var first = await service.CreateProductAsync(new CreateProductCommand(
            "SKU-BAR-01", "8991234567890", "Barang Barcode 1", null, null, cat.Id, unit.Id, 0, 100, TrackingMethod.None));
        first.Succeeded.Should().BeTrue();

        // 2. Create second product with identical barcode -> Should fail
        var duplicateCreate = await service.CreateProductAsync(new CreateProductCommand(
            "SKU-BAR-02", " 8991234567890 ", "Barang Barcode 2", null, null, cat.Id, unit.Id, 0, 100, TrackingMethod.None));
        duplicateCreate.Succeeded.Should().BeFalse();
        duplicateCreate.ErrorMessage.Should().Contain("sudah ada");

        // 3. Create second product with distinct barcode -> Should succeed
        var second = await service.CreateProductAsync(new CreateProductCommand(
            "SKU-BAR-02", "8990000000002", "Barang Barcode 2", null, null, cat.Id, unit.Id, 0, 100, TrackingMethod.None));
        second.Succeeded.Should().BeTrue();

        // 4. Update second product barcode to first product barcode -> Should fail
        var duplicateUpdate = await service.UpdateProductAsync(new UpdateProductCommand(
            second.Data!.Id, "SKU-BAR-02", "8991234567890", "Barang Barcode 2", null, null, cat.Id, unit.Id, 0, 100, TrackingMethod.None, true));
        duplicateUpdate.Succeeded.Should().BeFalse();
        duplicateUpdate.ErrorMessage.Should().Contain("sudah digunakan");
    }

    [Fact]
    public async Task ProductService_GetProducts_ShouldSupportFilterAndPagination()
    {
        using var context = CreateDbContext();
        var cat1 = new Category { Id = Guid.NewGuid(), Name = "Elektronik", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        var cat2 = new Category { Id = Guid.NewGuid(), Name = "Pakaian", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        var unit = new Unit { Id = Guid.NewGuid(), Name = "Pcs", Abbreviation = "PCS", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        context.Categories.AddRange(cat1, cat2);
        context.Units.Add(unit);

        context.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Sku = "ELK-01", Name = "Mouse A", CategoryId = cat1.Id, UnitId = unit.Id, IsActive = true, CreatedAtUtc = DateTime.UtcNow },
            new Product { Id = Guid.NewGuid(), Sku = "ELK-02", Name = "Mouse B", CategoryId = cat1.Id, UnitId = unit.Id, IsActive = true, CreatedAtUtc = DateTime.UtcNow },
            new Product { Id = Guid.NewGuid(), Sku = "PKN-01", Name = "Kemeja", CategoryId = cat2.Id, UnitId = unit.Id, IsActive = false, CreatedAtUtc = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new ProductService(context, new CreateProductValidator(), new UpdateProductValidator());

        // Test search
        var searchResult = await service.GetProductsAsync(new GetProductsQuery { Search = "Mouse" });
        searchResult.TotalCount.Should().Be(2);

        // Test category filter
        var categoryResult = await service.GetProductsAsync(new GetProductsQuery { CategoryId = cat2.Id });
        categoryResult.TotalCount.Should().Be(1);
        categoryResult.Items[0].Sku.Should().Be("PKN-01");

        // Test active status filter
        var activeOnlyResult = await service.GetProductsAsync(new GetProductsQuery { IsActive = true });
        activeOnlyResult.TotalCount.Should().Be(2);

        var inactiveOnlyResult = await service.GetProductsAsync(new GetProductsQuery { IsActive = false });
        inactiveOnlyResult.TotalCount.Should().Be(1);
        inactiveOnlyResult.Items[0].Sku.Should().Be("PKN-01");
    }
}

