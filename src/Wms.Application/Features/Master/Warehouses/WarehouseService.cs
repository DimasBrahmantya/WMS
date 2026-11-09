using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Domain.Entities.Master;

namespace Wms.Application.Features.Master.Warehouses;

public class WarehouseService : IWarehouseService
{
    private readonly IWmsDbContext _context;
    private readonly IValidator<CreateWarehouseCommand> _createWarehouseValidator;
    private readonly IValidator<UpdateWarehouseCommand> _updateWarehouseValidator;
    private readonly IValidator<CreateWarehouseLocationCommand> _createLocationValidator;
    private readonly IValidator<UpdateWarehouseLocationCommand> _updateLocationValidator;

    public WarehouseService(
        IWmsDbContext context,
        IValidator<CreateWarehouseCommand> createWarehouseValidator,
        IValidator<UpdateWarehouseCommand> updateWarehouseValidator,
        IValidator<CreateWarehouseLocationCommand> createLocationValidator,
        IValidator<UpdateWarehouseLocationCommand> updateLocationValidator)
    {
        _context = context;
        _createWarehouseValidator = createWarehouseValidator;
        _updateWarehouseValidator = updateWarehouseValidator;
        _createLocationValidator = createLocationValidator;
        _updateLocationValidator = updateLocationValidator;
    }

    public async Task<PaginatedList<WarehouseDto>> GetWarehousesAsync(GetWarehousesQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.Warehouses
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(w =>
                w.Code.ToLower().Contains(search) ||
                w.Name.ToLower().Contains(search) ||
                (w.Address != null && w.Address.ToLower().Contains(search)) ||
                (w.ContactPerson != null && w.ContactPerson.ToLower().Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(w => w.IsActive == query.IsActive.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        dbQuery = (query.SortBy?.ToLower(), query.SortDescending) switch
        {
            ("code", true) => dbQuery.OrderByDescending(w => w.Code),
            ("code", false) => dbQuery.OrderBy(w => w.Code),
            ("name", true) => dbQuery.OrderByDescending(w => w.Name),
            ("name", false) => dbQuery.OrderBy(w => w.Name),
            ("isactive", true) => dbQuery.OrderByDescending(w => w.IsActive),
            ("isactive", false) => dbQuery.OrderBy(w => w.IsActive),
            ("createdatutc", true) => dbQuery.OrderByDescending(w => w.CreatedAtUtc),
            ("createdatutc", false) => dbQuery.OrderBy(w => w.CreatedAtUtc),
            _ => dbQuery.OrderBy(w => w.Code)
        };

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(w => new WarehouseDto(
                w.Id,
                w.Code,
                w.Name,
                w.Address,
                w.ContactPerson,
                w.IsActive,
                w.Locations.Count,
                w.CreatedAtUtc,
                w.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PaginatedList<WarehouseDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<WarehouseDto>> GetActiveWarehousesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.IsActive)
            .OrderBy(w => w.Code)
            .Select(w => new WarehouseDto(
                w.Id,
                w.Code,
                w.Name,
                w.Address,
                w.ContactPerson,
                w.IsActive,
                w.Locations.Count,
                w.CreatedAtUtc,
                w.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<WarehouseDto>> GetWarehouseByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new WarehouseDto(
                w.Id,
                w.Code,
                w.Name,
                w.Address,
                w.ContactPerson,
                w.IsActive,
                w.Locations.Count,
                w.CreatedAtUtc,
                w.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (warehouse == null)
        {
            return Result<WarehouseDto>.Failure("Gudang tidak ditemukan atau Anda tidak memiliki akses.");
        }

        return Result<WarehouseDto>.Success(warehouse);
    }

    public async Task<Result<WarehouseDto>> CreateWarehouseAsync(CreateWarehouseCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _createWarehouseValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<WarehouseDto>.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var normalizedCode = command.Code.Trim();
        var exists = await _context.Warehouses
            .IgnoreQueryFilters()
            .AnyAsync(w => w.Code.ToLower() == normalizedCode.ToLower(), cancellationToken);

        if (exists)
        {
            return Result<WarehouseDto>.Failure($"Gudang dengan kode '{normalizedCode}' sudah ada.");
        }

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode.ToUpperInvariant(),
            Name = command.Name.Trim(),
            Address = command.Address?.Trim(),
            ContactPerson = command.ContactPerson?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Warehouses.Add(warehouse);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<WarehouseDto>.Success(new WarehouseDto(
            warehouse.Id,
            warehouse.Code,
            warehouse.Name,
            warehouse.Address,
            warehouse.ContactPerson,
            warehouse.IsActive,
            0,
            warehouse.CreatedAtUtc,
            warehouse.UpdatedAtUtc));
    }

    public async Task<Result> UpdateWarehouseAsync(UpdateWarehouseCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _updateWarehouseValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == command.Id, cancellationToken);
        if (warehouse == null)
        {
            return Result.Failure("Gudang tidak ditemukan atau Anda tidak memiliki akses.");
        }

        var normalizedCode = command.Code.Trim();
        var exists = await _context.Warehouses
            .IgnoreQueryFilters()
            .AnyAsync(w => w.Id != command.Id && w.Code.ToLower() == normalizedCode.ToLower(), cancellationToken);

        if (exists)
        {
            return Result.Failure($"Gudang dengan kode '{normalizedCode}' sudah digunakan.");
        }

        warehouse.Code = normalizedCode.ToUpperInvariant();
        warehouse.Name = command.Name.Trim();
        warehouse.Address = command.Address?.Trim();
        warehouse.ContactPerson = command.ContactPerson?.Trim();
        warehouse.IsActive = command.IsActive;
        warehouse.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteWarehouseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (warehouse == null)
        {
            return Result.Failure("Gudang tidak ditemukan.");
        }

        var hasLocations = await _context.WarehouseLocations.AnyAsync(l => l.WarehouseId == id, cancellationToken);
        if (hasLocations)
        {
            return Result.Failure("Gudang tidak dapat dihapus permanen karena masih memiliki lokasi rak. Silakan hapus lokasi terlebih dahulu atau nonaktifkan status gudang ini.");
        }

        var hasUserAssignments = await _context.UserWarehouses.AnyAsync(uw => uw.WarehouseId == id, cancellationToken);
        if (hasUserAssignments)
        {
            return Result.Failure("Gudang tidak dapat dihapus permanen karena masih ditugaskan kepada pengguna. Silakan lepaskan penugasan atau nonaktifkan status gudang ini.");
        }

        _context.Warehouses.Remove(warehouse);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ToggleWarehouseStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (warehouse == null)
        {
            return Result.Failure("Gudang tidak ditemukan.");
        }

        warehouse.IsActive = !warehouse.IsActive;
        warehouse.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // Locations
    public async Task<PaginatedList<WarehouseLocationDto>> GetLocationsAsync(GetWarehouseLocationsQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.WarehouseLocations
            .AsNoTracking()
            .Include(l => l.Warehouse)
            .AsQueryable();

        if (query.WarehouseId.HasValue)
        {
            dbQuery = dbQuery.Where(l => l.WarehouseId == query.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Zone))
        {
            var zone = query.Zone.Trim().ToLower();
            dbQuery = dbQuery.Where(l => l.Zone != null && l.Zone.ToLower() == zone);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(l =>
                l.Code.ToLower().Contains(search) ||
                l.Name.ToLower().Contains(search) ||
                (l.Zone != null && l.Zone.ToLower().Contains(search)) ||
                (l.Aisle != null && l.Aisle.ToLower().Contains(search)) ||
                (l.Rack != null && l.Rack.ToLower().Contains(search)) ||
                (l.Bin != null && l.Bin.ToLower().Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(l => l.IsActive == query.IsActive.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        dbQuery = (query.SortBy?.ToLower(), query.SortDescending) switch
        {
            ("code", true) => dbQuery.OrderByDescending(l => l.Code),
            ("code", false) => dbQuery.OrderBy(l => l.Code),
            ("name", true) => dbQuery.OrderByDescending(l => l.Name),
            ("name", false) => dbQuery.OrderBy(l => l.Name),
            ("zone", true) => dbQuery.OrderByDescending(l => l.Zone),
            ("zone", false) => dbQuery.OrderBy(l => l.Zone),
            ("isactive", true) => dbQuery.OrderByDescending(l => l.IsActive),
            ("isactive", false) => dbQuery.OrderBy(l => l.IsActive),
            _ => dbQuery.OrderBy(l => l.Warehouse.Name).ThenBy(l => l.Code)
        };

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(l => new WarehouseLocationDto(
                l.Id,
                l.WarehouseId,
                l.Warehouse.Name,
                l.Warehouse.Code,
                l.Code,
                l.Name,
                l.Zone,
                l.Aisle,
                l.Rack,
                l.Bin,
                l.IsActive,
                l.CreatedAtUtc,
                l.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PaginatedList<WarehouseLocationDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<WarehouseLocationDto>> GetActiveLocationsByWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        return await _context.WarehouseLocations
            .AsNoTracking()
            .Where(l => l.WarehouseId == warehouseId && l.IsActive)
            .OrderBy(l => l.Code)
            .Select(l => new WarehouseLocationDto(
                l.Id,
                l.WarehouseId,
                l.Warehouse.Name,
                l.Warehouse.Code,
                l.Code,
                l.Name,
                l.Zone,
                l.Aisle,
                l.Rack,
                l.Bin,
                l.IsActive,
                l.CreatedAtUtc,
                l.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<WarehouseLocationDto>> GetLocationByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var location = await _context.WarehouseLocations
            .AsNoTracking()
            .Include(l => l.Warehouse)
            .Where(l => l.Id == id)
            .Select(l => new WarehouseLocationDto(
                l.Id,
                l.WarehouseId,
                l.Warehouse.Name,
                l.Warehouse.Code,
                l.Code,
                l.Name,
                l.Zone,
                l.Aisle,
                l.Rack,
                l.Bin,
                l.IsActive,
                l.CreatedAtUtc,
                l.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (location == null)
        {
            return Result<WarehouseLocationDto>.Failure("Lokasi rak tidak ditemukan.");
        }

        return Result<WarehouseLocationDto>.Success(location);
    }

    public async Task<Result<WarehouseLocationDto>> CreateLocationAsync(CreateWarehouseLocationCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _createLocationValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<WarehouseLocationDto>.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken);
        if (warehouse == null)
        {
            return Result<WarehouseLocationDto>.Failure("Gudang tidak ditemukan atau Anda tidak memiliki akses.");
        }

        var normalizedCode = command.Code.Trim();
        var exists = await _context.WarehouseLocations
            .AnyAsync(l => l.WarehouseId == command.WarehouseId && l.Code.ToLower() == normalizedCode.ToLower(), cancellationToken);

        if (exists)
        {
            return Result<WarehouseLocationDto>.Failure($"Lokasi dengan kode '{normalizedCode}' sudah ada di gudang ini.");
        }

        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(),
            WarehouseId = command.WarehouseId,
            Code = normalizedCode.ToUpperInvariant(),
            Name = command.Name.Trim(),
            Zone = command.Zone?.Trim(),
            Aisle = command.Aisle?.Trim(),
            Rack = command.Rack?.Trim(),
            Bin = command.Bin?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.WarehouseLocations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<WarehouseLocationDto>.Success(new WarehouseLocationDto(
            location.Id,
            warehouse.Id,
            warehouse.Name,
            warehouse.Code,
            location.Code,
            location.Name,
            location.Zone,
            location.Aisle,
            location.Rack,
            location.Bin,
            location.IsActive,
            location.CreatedAtUtc,
            location.UpdatedAtUtc));
    }

    public async Task<Result> UpdateLocationAsync(UpdateWarehouseLocationCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _updateLocationValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var location = await _context.WarehouseLocations
            .Include(l => l.Warehouse)
            .FirstOrDefaultAsync(l => l.Id == command.Id, cancellationToken);

        if (location == null)
        {
            return Result.Failure("Lokasi rak tidak ditemukan.");
        }

        var normalizedCode = command.Code.Trim();
        var exists = await _context.WarehouseLocations
            .AnyAsync(l => l.WarehouseId == location.WarehouseId && l.Id != command.Id && l.Code.ToLower() == normalizedCode.ToLower(), cancellationToken);

        if (exists)
        {
            return Result.Failure($"Lokasi dengan kode '{normalizedCode}' sudah digunakan di gudang ini.");
        }

        location.Code = normalizedCode.ToUpperInvariant();
        location.Name = command.Name.Trim();
        location.Zone = command.Zone?.Trim();
        location.Aisle = command.Aisle?.Trim();
        location.Rack = command.Rack?.Trim();
        location.Bin = command.Bin?.Trim();
        location.IsActive = command.IsActive;
        location.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteLocationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var location = await _context.WarehouseLocations.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (location == null)
        {
            return Result.Failure("Lokasi rak tidak ditemukan.");
        }

        _context.WarehouseLocations.Remove(location);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ToggleLocationStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var location = await _context.WarehouseLocations.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (location == null)
        {
            return Result.Failure("Lokasi rak tidak ditemukan.");
        }

        location.IsActive = !location.IsActive;
        location.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

