using FluentAssertions;
using Wms.Application.Features.Master.Categories;
using Wms.Application.Features.Master.Customers;
using Wms.Application.Features.Master.Products;
using Wms.Application.Features.Master.Suppliers;
using Wms.Application.Features.Master.Units;
using Wms.Application.Features.Master.Warehouses;
using Wms.Domain.Enums;
using Xunit;

namespace Wms.Application.Tests.Features.Master;

public class MasterValidatorTests
{
    [Fact]
    public void CreateCategoryValidator_ShouldFail_WhenNameIsEmpty()
    {
        var validator = new CreateCategoryValidator();
        var result = validator.Validate(new CreateCategoryCommand("", "Deskripsi"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void CreateCategoryValidator_ShouldPass_WhenValid()
    {
        var validator = new CreateCategoryValidator();
        var result = validator.Validate(new CreateCategoryCommand("Elektronik", "Komponen dan aksesoris"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateUnitValidator_ShouldFail_WhenAbbreviationIsEmpty()
    {
        var validator = new CreateUnitValidator();
        var result = validator.Validate(new CreateUnitCommand("Pieces", ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Abbreviation");
    }

    [Fact]
    public void CreateProductValidator_ShouldFail_WhenMinStockIsNegative()
    {
        var validator = new CreateProductValidator();
        var result = validator.Validate(new CreateProductCommand(
            "SKU-001",
            "899123456",
            "Barang A",
            "Deskripsi",
            "Merek A",
            Guid.NewGuid(),
            Guid.NewGuid(),
            -5,
            100,
            TrackingMethod.None));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MinStock");
    }

    [Fact]
    public void CreateProductValidator_ShouldFail_WhenMaxStockIsLessThanMinStock()
    {
        var validator = new CreateProductValidator();
        var result = validator.Validate(new CreateProductCommand(
            "SKU-001",
            "899123456",
            "Barang A",
            "Deskripsi",
            "Merek A",
            Guid.NewGuid(),
            Guid.NewGuid(),
            50,
            20,
            TrackingMethod.None));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxStock");
    }

    [Fact]
    public void CreateWarehouseValidator_ShouldFail_WhenCodeOrNameIsEmpty()
    {
        var validator = new CreateWarehouseValidator();
        var result = validator.Validate(new CreateWarehouseCommand("", "", null, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Code");
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void CreateSupplierValidator_ShouldFail_WhenEmailIsInvalid()
    {
        var validator = new CreateSupplierValidator();
        var result = validator.Validate(new CreateSupplierCommand(
            "SUP-001",
            "PT Vendor",
            "Budi",
            "bukan-email-valid",
            "021-1234",
            "Alamat",
            "NPWP",
            null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void CreateCustomerValidator_ShouldFail_WhenCodeIsEmpty()
    {
        var validator = new CreateCustomerValidator();
        var result = validator.Validate(new CreateCustomerCommand(
            "",
            "Pelanggan A",
            "Kontak",
            "cust@mail.com",
            "08123",
            "Alamat",
            null,
            null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Code");
    }
}

