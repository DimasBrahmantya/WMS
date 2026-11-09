using FluentValidation;

namespace Wms.Application.Features.Master.Warehouses;

public class CreateWarehouseValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Kode gudang wajib diisi.")
            .MaximumLength(50).WithMessage("Kode gudang maksimal 50 karakter.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama gudang wajib diisi.")
            .MaximumLength(200).WithMessage("Nama gudang maksimal 200 karakter.");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Alamat maksimal 500 karakter.");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(100).WithMessage("Penanggung jawab maksimal 100 karakter.");
    }
}

public class UpdateWarehouseValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id gudang tidak boleh kosong.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Kode gudang wajib diisi.")
            .MaximumLength(50).WithMessage("Kode gudang maksimal 50 karakter.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama gudang wajib diisi.")
            .MaximumLength(200).WithMessage("Nama gudang maksimal 200 karakter.");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Alamat maksimal 500 karakter.");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(100).WithMessage("Penanggung jawab maksimal 100 karakter.");
    }
}

public class CreateWarehouseLocationValidator : AbstractValidator<CreateWarehouseLocationCommand>
{
    public CreateWarehouseLocationValidator()
    {
        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Gudang wajib dipilih.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Kode lokasi wajib diisi.")
            .MaximumLength(50).WithMessage("Kode lokasi maksimal 50 karakter.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama lokasi wajib diisi.")
            .MaximumLength(100).WithMessage("Nama lokasi maksimal 100 karakter.");

        RuleFor(x => x.Zone).MaximumLength(50).WithMessage("Zona maksimal 50 karakter.");
        RuleFor(x => x.Aisle).MaximumLength(50).WithMessage("Lorong (Aisle) maksimal 50 karakter.");
        RuleFor(x => x.Rack).MaximumLength(50).WithMessage("Rak maksimal 50 karakter.");
        RuleFor(x => x.Bin).MaximumLength(50).WithMessage("Bin/Kotak maksimal 50 karakter.");
    }
}

public class UpdateWarehouseLocationValidator : AbstractValidator<UpdateWarehouseLocationCommand>
{
    public UpdateWarehouseLocationValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id lokasi tidak boleh kosong.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Kode lokasi wajib diisi.")
            .MaximumLength(50).WithMessage("Kode lokasi maksimal 50 karakter.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama lokasi wajib diisi.")
            .MaximumLength(100).WithMessage("Nama lokasi maksimal 100 karakter.");

        RuleFor(x => x.Zone).MaximumLength(50).WithMessage("Zona maksimal 50 karakter.");
        RuleFor(x => x.Aisle).MaximumLength(50).WithMessage("Lorong (Aisle) maksimal 50 karakter.");
        RuleFor(x => x.Rack).MaximumLength(50).WithMessage("Rak maksimal 50 karakter.");
        RuleFor(x => x.Bin).MaximumLength(50).WithMessage("Bin/Kotak maksimal 50 karakter.");
    }
}

