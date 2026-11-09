using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Domain.Entities.Master;

namespace Wms.Application.Features.Master.Suppliers;

public class SupplierService : ISupplierService
{
    private readonly IWmsDbContext _context;
    private readonly IValidator<CreateSupplierCommand> _createValidator;
    private readonly IValidator<UpdateSupplierCommand> _updateValidator;

    public SupplierService(
        IWmsDbContext context,
        IValidator<CreateSupplierCommand> createValidator,
        IValidator<UpdateSupplierCommand> updateValidator)
    {
        _context = context;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PaginatedList<SupplierDto>> GetSuppliersAsync(GetSuppliersQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.Suppliers
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(s =>
                s.Code.ToLower().Contains(search) ||
                s.Name.ToLower().Contains(search) ||
                (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(search)) ||
                (s.Email != null && s.Email.ToLower().Contains(search)) ||
                (s.Phone != null && s.Phone.ToLower().Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(s => s.IsActive == query.IsActive.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        dbQuery = (query.SortBy?.ToLower(), query.SortDescending) switch
        {
            ("code", true) => dbQuery.OrderByDescending(s => s.Code),
            ("code", false) => dbQuery.OrderBy(s => s.Code),
            ("name", true) => dbQuery.OrderByDescending(s => s.Name),
            ("name", false) => dbQuery.OrderBy(s => s.Name),
            ("contactperson", true) => dbQuery.OrderByDescending(s => s.ContactPerson),
            ("contactperson", false) => dbQuery.OrderBy(s => s.ContactPerson),
            ("isactive", true) => dbQuery.OrderByDescending(s => s.IsActive),
            ("isactive", false) => dbQuery.OrderBy(s => s.IsActive),
            ("createdatutc", true) => dbQuery.OrderByDescending(s => s.CreatedAtUtc),
            ("createdatutc", false) => dbQuery.OrderBy(s => s.CreatedAtUtc),
            _ => dbQuery.OrderBy(s => s.Code)
        };

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new SupplierDto(
                s.Id,
                s.Code,
                s.Name,
                s.ContactPerson,
                s.Email,
                s.Phone,
                s.Address,
                s.TaxNumber,
                s.Notes,
                s.IsActive,
                s.CreatedAtUtc,
                s.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PaginatedList<SupplierDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<SupplierDto>> GetActiveSuppliersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Suppliers
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(
                s.Id,
                s.Code,
                s.Name,
                s.ContactPerson,
                s.Email,
                s.Phone,
                s.Address,
                s.TaxNumber,
                s.Notes,
                s.IsActive,
                s.CreatedAtUtc,
                s.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<SupplierDto>> GetSupplierByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SupplierDto(
                s.Id,
                s.Code,
                s.Name,
                s.ContactPerson,
                s.Email,
                s.Phone,
                s.Address,
                s.TaxNumber,
                s.Notes,
                s.IsActive,
                s.CreatedAtUtc,
                s.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (supplier == null)
        {
            return Result<SupplierDto>.Failure("Pemasok tidak ditemukan.");
        }

        return Result<SupplierDto>.Success(supplier);
    }

    public async Task<Result<SupplierDto>> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<SupplierDto>.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        var exists = await _context.Suppliers
            .AnyAsync(s => s.Code.ToLower() == normalizedCode.ToLower(), cancellationToken);

        if (exists)
        {
            return Result<SupplierDto>.Failure($"Pemasok dengan kode '{normalizedCode}' sudah ada.");
        }

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = command.Name.Trim(),
            ContactPerson = command.ContactPerson?.Trim(),
            Email = command.Email?.Trim(),
            Phone = command.Phone?.Trim(),
            Address = command.Address?.Trim(),
            TaxNumber = command.TaxNumber?.Trim(),
            Notes = command.Notes?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<SupplierDto>.Success(new SupplierDto(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.TaxNumber,
            supplier.Notes,
            supplier.IsActive,
            supplier.CreatedAtUtc,
            supplier.UpdatedAtUtc));
    }

    public async Task<Result> UpdateSupplierAsync(UpdateSupplierCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);
        if (supplier == null)
        {
            return Result.Failure("Pemasok tidak ditemukan.");
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        var exists = await _context.Suppliers
            .AnyAsync(s => s.Id != command.Id && s.Code.ToLower() == normalizedCode.ToLower(), cancellationToken);

        if (exists)
        {
            return Result.Failure($"Pemasok dengan kode '{normalizedCode}' sudah digunakan.");
        }

        supplier.Code = normalizedCode;
        supplier.Name = command.Name.Trim();
        supplier.ContactPerson = command.ContactPerson?.Trim();
        supplier.Email = command.Email?.Trim();
        supplier.Phone = command.Phone?.Trim();
        supplier.Address = command.Address?.Trim();
        supplier.TaxNumber = command.TaxNumber?.Trim();
        supplier.Notes = command.Notes?.Trim();
        supplier.IsActive = command.IsActive;
        supplier.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteSupplierAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier == null)
        {
            return Result.Failure("Pemasok tidak ditemukan.");
        }

        // In later phases, check if referenced by Purchase Orders or Stock In
        _context.Suppliers.Remove(supplier);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ToggleSupplierStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier == null)
        {
            return Result.Failure("Pemasok tidak ditemukan.");
        }

        supplier.IsActive = !supplier.IsActive;
        supplier.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

