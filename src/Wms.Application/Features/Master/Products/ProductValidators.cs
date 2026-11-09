using FluentValidation;

namespace Wms.Application.Features.Master.Products;

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU barang wajib diisi.")
            .MaximumLength(100).WithMessage("SKU maksimal 100 karakter.");

        RuleFor(x => x.Barcode)
            .MaximumLength(100).WithMessage("Barcode maksimal 100 karakter.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama barang wajib diisi.")
            .MaximumLength(200).WithMessage("Nama barang maksimal 200 karakter.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Deskripsi maksimal 1000 karakter.");

        RuleFor(x => x.Brand)
            .MaximumLength(100).WithMessage("Merek maksimal 100 karakter.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Kategori wajib dipilih.");

        RuleFor(x => x.UnitId)
            .NotEmpty().WithMessage("Satuan dasar wajib dipilih.");

        RuleFor(x => x.MinStock)
            .GreaterThanOrEqualTo(0).WithMessage("Stok minimum tidak boleh negatif.");

        RuleFor(x => x.MaxStock)
            .GreaterThanOrEqualTo(x => x.MinStock)
            .When(x => x.MaxStock.HasValue)
            .WithMessage("Stok maksimum harus lebih besar atau sama dengan stok minimum.");
    }
}

public class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id barang tidak boleh kosong.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU barang wajib diisi.")
            .MaximumLength(100).WithMessage("SKU maksimal 100 karakter.");

        RuleFor(x => x.Barcode)
            .MaximumLength(100).WithMessage("Barcode maksimal 100 karakter.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama barang wajib diisi.")
            .MaximumLength(200).WithMessage("Nama barang maksimal 200 karakter.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Deskripsi maksimal 1000 karakter.");

        RuleFor(x => x.Brand)
            .MaximumLength(100).WithMessage("Merek maksimal 100 karakter.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Kategori wajib dipilih.");

        RuleFor(x => x.UnitId)
            .NotEmpty().WithMessage("Satuan dasar wajib dipilih.");

        RuleFor(x => x.MinStock)
            .GreaterThanOrEqualTo(0).WithMessage("Stok minimum tidak boleh negatif.");

        RuleFor(x => x.MaxStock)
            .GreaterThanOrEqualTo(x => x.MinStock)
            .When(x => x.MaxStock.HasValue)
            .WithMessage("Stok maksimum harus lebih besar atau sama dengan stok minimum.");
    }
}

