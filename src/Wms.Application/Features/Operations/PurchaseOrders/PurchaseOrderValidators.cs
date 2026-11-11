using FluentValidation;

namespace Wms.Application.Features.Operations.PurchaseOrders;

public class CreatePurchaseOrderValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Pemasok wajib dipilih.");

        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Gudang tujuan wajib dipilih.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Daftar barang PO tidak boleh kosong.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId)
                .NotEmpty().WithMessage("Barang wajib dipilih.");

            line.RuleFor(l => l.OrderedQuantity)
                .GreaterThan(0).WithMessage("Kuantitas pesanan harus lebih besar dari 0.");

            line.RuleFor(l => l.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Harga satuan tidak boleh negatif.");
        });
    }
}

public class UpdatePurchaseOrderValidator : AbstractValidator<UpdatePurchaseOrderCommand>
{
    public UpdatePurchaseOrderValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Pemasok wajib dipilih.");

        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Gudang tujuan wajib dipilih.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Daftar barang PO tidak boleh kosong.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId)
                .NotEmpty().WithMessage("Barang wajib dipilih.");

            line.RuleFor(l => l.OrderedQuantity)
                .GreaterThan(0).WithMessage("Kuantitas pesanan harus lebih besar dari 0.");

            line.RuleFor(l => l.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Harga satuan tidak boleh negatif.");
        });
    }
}

