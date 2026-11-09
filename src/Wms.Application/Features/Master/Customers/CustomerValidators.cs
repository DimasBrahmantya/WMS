using FluentValidation;

namespace Wms.Application.Features.Master.Customers;

public class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Kode pelanggan wajib diisi.")
            .MaximumLength(50).WithMessage("Kode pelanggan maksimal 50 karakter.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama pelanggan wajib diisi.")
            .MaximumLength(200).WithMessage("Nama pelanggan maksimal 200 karakter.");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(100).WithMessage("Kontak person maksimal 100 karakter.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Format email tidak valid.")
            .MaximumLength(100).WithMessage("Email maksimal 100 karakter.");

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("Nomor telepon maksimal 50 karakter.");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Alamat maksimal 500 karakter.");

        RuleFor(x => x.TaxNumber)
            .MaximumLength(50).WithMessage("NPWP maksimal 50 karakter.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Catatan maksimal 1000 karakter.");
    }
}

public class UpdateCustomerValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id pelanggan tidak boleh kosong.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Kode pelanggan wajib diisi.")
            .MaximumLength(50).WithMessage("Kode pelanggan maksimal 50 karakter.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama pelanggan wajib diisi.")
            .MaximumLength(200).WithMessage("Nama pelanggan maksimal 200 karakter.");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(100).WithMessage("Kontak person maksimal 100 karakter.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Format email tidak valid.")
            .MaximumLength(100).WithMessage("Email maksimal 100 karakter.");

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("Nomor telepon maksimal 50 karakter.");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Alamat maksimal 500 karakter.");

        RuleFor(x => x.TaxNumber)
            .MaximumLength(50).WithMessage("NPWP maksimal 50 karakter.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Catatan maksimal 1000 karakter.");
    }
}

