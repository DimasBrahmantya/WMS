using FluentValidation;

namespace Wms.Application.Features.Master.Categories;

public class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama kategori wajib diisi.")
            .MaximumLength(100).WithMessage("Nama kategori maksimal 100 karakter.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Deskripsi maksimal 500 karakter.");
    }
}

public class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id kategori tidak boleh kosong.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama kategori wajib diisi.")
            .MaximumLength(100).WithMessage("Nama kategori maksimal 100 karakter.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Deskripsi maksimal 500 karakter.");
    }
}

