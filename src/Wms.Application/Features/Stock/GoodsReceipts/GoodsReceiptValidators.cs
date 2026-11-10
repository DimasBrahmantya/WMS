using FluentValidation;

namespace Wms.Application.Features.Stock.GoodsReceipts;

public class CreateGoodsReceiptValidator : AbstractValidator<CreateGoodsReceiptCommand>
{
    public CreateGoodsReceiptValidator()
    {
        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Gudang tujuan wajib dipilih.");

        RuleFor(x => x.DocumentDate)
            .NotEmpty().WithMessage("Tanggal dokumen wajib diisi.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Minimal satu baris barang harus dimasukkan.");

        RuleForEach(x => x.Lines).SetValidator(new CreateGoodsReceiptLineValidator());
    }
}

public class CreateGoodsReceiptLineValidator : AbstractValidator<CreateGoodsReceiptLineDto>
{
    public CreateGoodsReceiptLineValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Barang wajib dipilih.");

        RuleFor(x => x.WarehouseLocationId)
            .NotEmpty().WithMessage("Lokasi rak wajib dipilih.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Kuantitas barang masuk harus lebih besar dari 0.");

        RuleFor(x => x.UnitCost)
            .GreaterThanOrEqualTo(0).WithMessage("Biaya satuan barang tidak boleh negatif.");
    }
}

public class UpdateGoodsReceiptValidator : AbstractValidator<UpdateGoodsReceiptCommand>
{
    public UpdateGoodsReceiptValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID penerimaan tidak valid.");

        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Gudang tujuan wajib dipilih.");

        RuleFor(x => x.DocumentDate)
            .NotEmpty().WithMessage("Tanggal dokumen wajib diisi.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Minimal satu baris barang harus dimasukkan.");

        RuleForEach(x => x.Lines).SetValidator(new CreateGoodsReceiptLineValidator());
    }
}

