using FluentValidation;

namespace Wms.Application.Features.Master.Suppliers;

public class CreateSupplierValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Kode pemasok wajib diisi.")
            .MaximumLength(50).WithMessage("Kode pemasok maksimal 50 karakter.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama pemasok wajib diisi.")
            .MaximumLength(200).WithMessage("Nama pemasok maksimal 200 karakter.");

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

public class UpdateSupplierValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id pemasok tidak boleh kosong.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Kode pemasok wajib diisi.")
            .MaximumLength(50).WithMessage("Kode pemasok maksimal 50 karakter.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama pemasok wajib diisi.")
            .MaximumLength(200).WithMessage("Nama pemasok maksimal 200 karakter.");

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

