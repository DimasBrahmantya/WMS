using FluentValidation;

namespace Wms.Application.Features.Stock.StockOuts;

public class CreateStockOutValidator : AbstractValidator<CreateStockOutCommand>
{
    public CreateStockOutValidator()
    {
        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Gudang sumber wajib dipilih.");

        RuleFor(x => x.DocumentDate)
            .NotEmpty().WithMessage("Tanggal dokumen wajib diisi.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Minimal satu baris barang harus dikeluarkan.");

        RuleForEach(x => x.Lines).SetValidator(new CreateStockOutLineValidator());
    }
}

public class CreateStockOutLineValidator : AbstractValidator<CreateStockOutLineDto>
{
    public CreateStockOutLineValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Barang wajib dipilih.");

        RuleFor(x => x.WarehouseLocationId)
            .NotEmpty().WithMessage("Lokasi rak sumber wajib dipilih.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Kuantitas barang keluar harus lebih besar dari 0.");
    }
}

public class UpdateStockOutValidator : AbstractValidator<UpdateStockOutCommand>
{
    public UpdateStockOutValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID pengeluaran tidak valid.");

        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Gudang sumber wajib dipilih.");

        RuleFor(x => x.DocumentDate)
            .NotEmpty().WithMessage("Tanggal dokumen wajib diisi.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Minimal satu baris barang harus dikeluarkan.");

        RuleForEach(x => x.Lines).SetValidator(new CreateStockOutLineValidator());
    }
}

