using FluentValidation;

namespace Wms.Application.Features.Operations.Transfers;

public class CreateStockTransferValidator : AbstractValidator<CreateStockTransferCommand>
{
    public CreateStockTransferValidator()
    {
        RuleFor(x => x.OriginWarehouseId)
            .NotEmpty().WithMessage("Gudang asal wajib dipilih.");

        RuleFor(x => x.DestinationWarehouseId)
            .NotEmpty().WithMessage("Gudang tujuan wajib dipilih.")
            .Must((cmd, destId) => destId != cmd.OriginWarehouseId)
            .WithMessage("Gudang tujuan tidak boleh sama dengan gudang asal.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Daftar barang transfer tidak boleh kosong.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId)
                .NotEmpty().WithMessage("Barang wajib dipilih.");

            line.RuleFor(l => l.QuantityRequested)
                .GreaterThan(0).WithMessage("Kuantitas transfer harus lebih besar dari 0.");
        });
    }
}

