using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Domain.Entities.Master;

namespace Wms.Application.Features.Master.Customers;

public class CustomerService : ICustomerService
{
    private readonly IWmsDbContext _context;
    private readonly IValidator<CreateCustomerCommand> _createValidator;
    private readonly IValidator<UpdateCustomerCommand> _updateValidator;

    public CustomerService(
        IWmsDbContext context,
        IValidator<CreateCustomerCommand> createValidator,
        IValidator<UpdateCustomerCommand> updateValidator)
    {
        _context = context;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PaginatedList<CustomerDto>> GetCustomersAsync(GetCustomersQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.Customers
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(c =>
                c.Code.ToLower().Contains(search) ||
                c.Name.ToLower().Contains(search) ||
                (c.ContactPerson != null && c.ContactPerson.ToLower().Contains(search)) ||
                (c.Email != null && c.Email.ToLower().Contains(search)) ||
                (c.Phone != null && c.Phone.ToLower().Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.IsActive == query.IsActive.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        dbQuery = (query.SortBy?.ToLower(), query.SortDescending) switch
        {
            ("code", true) => dbQuery.OrderByDescending(c => c.Code),
            ("code", false) => dbQuery.OrderBy(c => c.Code),
            ("name", true) => dbQuery.OrderByDescending(c => c.Name),
            ("name", false) => dbQuery.OrderBy(c => c.Name),
            ("contactperson", true) => dbQuery.OrderByDescending(c => c.ContactPerson),
            ("contactperson", false) => dbQuery.OrderBy(c => c.ContactPerson),
            ("isactive", true) => dbQuery.OrderByDescending(c => c.IsActive),
            ("isactive", false) => dbQuery.OrderBy(c => c.IsActive),
            ("createdatutc", true) => dbQuery.OrderByDescending(c => c.CreatedAtUtc),
            ("createdatutc", false) => dbQuery.OrderBy(c => c.CreatedAtUtc),
            _ => dbQuery.OrderBy(c => c.Code)
        };

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new CustomerDto(
                c.Id,
                c.Code,
                c.Name,
                c.ContactPerson,
                c.Email,
                c.Phone,
                c.Address,
                c.TaxNumber,
                c.Notes,
                c.IsActive,
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PaginatedList<CustomerDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<CustomerDto>> GetActiveCustomersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CustomerDto(
                c.Id,
                c.Code,
                c.Name,
                c.ContactPerson,
                c.Email,
                c.Phone,
                c.Address,
                c.TaxNumber,
                c.Notes,
                c.IsActive,
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<CustomerDto>> GetCustomerByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CustomerDto(
                c.Id,
                c.Code,
                c.Name,
                c.ContactPerson,
                c.Email,
                c.Phone,
                c.Address,
                c.TaxNumber,
                c.Notes,
                c.IsActive,
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (customer == null)
        {
            return Result<CustomerDto>.Failure("Pelanggan tidak ditemukan.");
        }

        return Result<CustomerDto>.Success(customer);
    }

    public async Task<Result<CustomerDto>> CreateCustomerAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CustomerDto>.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        var exists = await _context.Customers
            .AnyAsync(c => c.Code.ToLower() == normalizedCode.ToLower(), cancellationToken);

        if (exists)
        {
            return Result<CustomerDto>.Failure($"Pelanggan dengan kode '{normalizedCode}' sudah ada.");
        }

        var customer = new Customer
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

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<CustomerDto>.Success(new CustomerDto(
            customer.Id,
            customer.Code,
            customer.Name,
            customer.ContactPerson,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.TaxNumber,
            customer.Notes,
            customer.IsActive,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc));
    }

    public async Task<Result> UpdateCustomerAsync(UpdateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (customer == null)
        {
            return Result.Failure("Pelanggan tidak ditemukan.");
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        var exists = await _context.Customers
            .AnyAsync(c => c.Id != command.Id && c.Code.ToLower() == normalizedCode.ToLower(), cancellationToken);

        if (exists)
        {
            return Result.Failure($"Pelanggan dengan kode '{normalizedCode}' sudah digunakan.");
        }

        customer.Code = normalizedCode;
        customer.Name = command.Name.Trim();
        customer.ContactPerson = command.ContactPerson?.Trim();
        customer.Email = command.Email?.Trim();
        customer.Phone = command.Phone?.Trim();
        customer.Address = command.Address?.Trim();
        customer.TaxNumber = command.TaxNumber?.Trim();
        customer.Notes = command.Notes?.Trim();
        customer.IsActive = command.IsActive;
        customer.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteCustomerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer == null)
        {
            return Result.Failure("Pelanggan tidak ditemukan.");
        }

        // In later phases, check if referenced by Sales Orders or Stock Out
        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ToggleCustomerStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer == null)
        {
            return Result.Failure("Pelanggan tidak ditemukan.");
        }

        customer.IsActive = !customer.IsActive;
        customer.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

