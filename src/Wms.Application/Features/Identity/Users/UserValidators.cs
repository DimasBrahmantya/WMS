using FluentValidation;

namespace Wms.Application.Features.Identity.Users;

public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username wajib diisi.")
            .MinimumLength(3).WithMessage("Username minimal 3 karakter.")
            .MaximumLength(50).WithMessage("Username maksimal 50 karakter.")
            .Matches("^[a-zA-Z0-9_.-]+$").WithMessage("Username hanya boleh mengandung huruf, angka, titik, strip, dan underscore.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email wajib diisi.")
            .EmailAddress().WithMessage("Format email tidak valid.")
            .MaximumLength(100).WithMessage("Email maksimal 100 karakter.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Nama lengkap wajib diisi.")
            .MaximumLength(200).WithMessage("Nama lengkap maksimal 200 karakter.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password wajib diisi.")
            .MinimumLength(8).WithMessage("Password minimal 8 karakter.")
            .Matches("[A-Z]").WithMessage("Password harus mengandung huruf besar.")
            .Matches("[a-z]").WithMessage("Password harus mengandung huruf kecil.")
            .Matches("[0-9]").WithMessage("Password harus mengandung angka.");

        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage("Minimal satu peran (role) harus dipilih.");
    }
}

public class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id user tidak boleh kosong.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email wajib diisi.")
            .EmailAddress().WithMessage("Format email tidak valid.")
            .MaximumLength(100).WithMessage("Email maksimal 100 karakter.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Nama lengkap wajib diisi.")
            .MaximumLength(200).WithMessage("Nama lengkap maksimal 200 karakter.");

        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage("Minimal satu peran (role) harus dipilih.");
    }
}

