using FluentValidation;

namespace Wms.Application.Features.Identity.Roles;

public class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama peran (role) wajib diisi.")
            .MaximumLength(100).WithMessage("Nama peran maksimal 100 karakter.")
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("Nama peran hanya boleh berupa karakter alfanumerik dan underscore.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Deskripsi maksimal 500 karakter.");
    }
}

public class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id peran tidak boleh kosong.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Deskripsi maksimal 500 karakter.");
    }
}

