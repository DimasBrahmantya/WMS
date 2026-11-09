using FluentValidation;

namespace Wms.Application.Features.Master.Units;

public class CreateUnitValidator : AbstractValidator<CreateUnitCommand>
{
    public CreateUnitValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama satuan wajib diisi.")
            .MaximumLength(100).WithMessage("Nama satuan maksimal 100 karakter.");

        RuleFor(x => x.Abbreviation)
            .NotEmpty().WithMessage("Singkatan satuan wajib diisi.")
            .MaximumLength(20).WithMessage("Singkatan satuan maksimal 20 karakter.");
    }
}

public class UpdateUnitValidator : AbstractValidator<UpdateUnitCommand>
{
    public UpdateUnitValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id satuan tidak boleh kosong.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama satuan wajib diisi.")
            .MaximumLength(100).WithMessage("Nama satuan maksimal 100 karakter.");

        RuleFor(x => x.Abbreviation)
            .NotEmpty().WithMessage("Singkatan satuan wajib diisi.")
            .MaximumLength(20).WithMessage("Singkatan satuan maksimal 20 karakter.");
    }
}

