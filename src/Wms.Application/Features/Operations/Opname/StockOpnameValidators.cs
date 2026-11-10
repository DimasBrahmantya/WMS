using FluentValidation;

namespace Wms.Application.Features.Operations.Opname;

public class CreateStockOpnameSessionValidator : AbstractValidator<CreateStockOpnameSessionCommand>
{
    public CreateStockOpnameSessionValidator()
    {
        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Gudang opname wajib dipilih.");
    }
}
