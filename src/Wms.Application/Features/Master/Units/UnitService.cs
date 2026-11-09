using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Domain.Entities.Master;

namespace Wms.Application.Features.Master.Units;

public class UnitService : IUnitService
{
    private readonly IWmsDbContext _context;
    private readonly IValidator<CreateUnitCommand> _createValidator;
    private readonly IValidator<UpdateUnitCommand> _updateValidator;

    public UnitService(
        IWmsDbContext context,
        IValidator<CreateUnitCommand> createValidator,
        IValidator<UpdateUnitCommand> updateValidator)
    {
        _context = context;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PaginatedList<UnitDto>> GetUnitsAsync(GetUnitsQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.Units
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(u => u.Name.ToLower().Contains(search) || u.Abbreviation.ToLower().Contains(search));
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(u => u.IsActive == query.IsActive.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        dbQuery = (query.SortBy?.ToLower(), query.SortDescending) switch
        {
            ("name", true) => dbQuery.OrderByDescending(u => u.Name),
            ("name", false) => dbQuery.OrderBy(u => u.Name),
            ("abbreviation", true) => dbQuery.OrderByDescending(u => u.Abbreviation),
            ("abbreviation", false) => dbQuery.OrderBy(u => u.Abbreviation),
            ("isactive", true) => dbQuery.OrderByDescending(u => u.IsActive),
            ("isactive", false) => dbQuery.OrderBy(u => u.IsActive),
            ("createdatutc", true) => dbQuery.OrderByDescending(u => u.CreatedAtUtc),
            ("createdatutc", false) => dbQuery.OrderBy(u => u.CreatedAtUtc),
            _ => dbQuery.OrderBy(u => u.Name)
        };

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new UnitDto(
                u.Id,
                u.Name,
                u.Abbreviation,
                u.IsActive,
                u.Products.Count,
                u.CreatedAtUtc,
                u.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PaginatedList<UnitDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<UnitDto>> GetActiveUnitsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Units
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new UnitDto(
                u.Id,
                u.Name,
                u.Abbreviation,
                u.IsActive,
                u.Products.Count,
                u.CreatedAtUtc,
                u.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<UnitDto>> GetUnitByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var unit = await _context.Units
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UnitDto(
                u.Id,
                u.Name,
                u.Abbreviation,
                u.IsActive,
                u.Products.Count,
                u.CreatedAtUtc,
                u.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (unit == null)
        {
            return Result<UnitDto>.Failure("Satuan tidak ditemukan.");
        }

        return Result<UnitDto>.Success(unit);
    }

    public async Task<Result<UnitDto>> CreateUnitAsync(CreateUnitCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<UnitDto>.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var normalizedName = command.Name.Trim();
        var normalizedAbbr = command.Abbreviation.Trim();

        var nameExists = await _context.Units.AnyAsync(u => u.Name.ToLower() == normalizedName.ToLower(), cancellationToken);
        if (nameExists)
        {
            return Result<UnitDto>.Failure($"Satuan dengan nama '{normalizedName}' sudah ada.");
        }

        var abbrExists = await _context.Units.AnyAsync(u => u.Abbreviation.ToLower() == normalizedAbbr.ToLower(), cancellationToken);
        if (abbrExists)
        {
            return Result<UnitDto>.Failure($"Satuan dengan singkatan '{normalizedAbbr}' sudah ada.");
        }

        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Abbreviation = normalizedAbbr,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Units.Add(unit);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<UnitDto>.Success(new UnitDto(
            unit.Id,
            unit.Name,
            unit.Abbreviation,
            unit.IsActive,
            0,
            unit.CreatedAtUtc,
            unit.UpdatedAtUtc));
    }

    public async Task<Result> UpdateUnitAsync(UpdateUnitCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);
        if (unit == null)
        {
            return Result.Failure("Satuan tidak ditemukan.");
        }

        var normalizedName = command.Name.Trim();
        var normalizedAbbr = command.Abbreviation.Trim();

        var nameExists = await _context.Units.AnyAsync(u => u.Id != command.Id && u.Name.ToLower() == normalizedName.ToLower(), cancellationToken);
        if (nameExists)
        {
            return Result.Failure($"Satuan dengan nama '{normalizedName}' sudah digunakan.");
        }

        var abbrExists = await _context.Units.AnyAsync(u => u.Id != command.Id && u.Abbreviation.ToLower() == normalizedAbbr.ToLower(), cancellationToken);
        if (abbrExists)
        {
            return Result.Failure($"Satuan dengan singkatan '{normalizedAbbr}' sudah digunakan.");
        }

        unit.Name = normalizedName;
        unit.Abbreviation = normalizedAbbr;
        unit.IsActive = command.IsActive;
        unit.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteUnitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (unit == null)
        {
            return Result.Failure("Satuan tidak ditemukan.");
        }

        var isReferenced = await _context.Products.AnyAsync(p => p.UnitId == id, cancellationToken);
        if (isReferenced)
        {
            return Result.Failure("Satuan tidak dapat dihapus permanen karena masih digunakan oleh produk. Silakan nonaktifkan status satuan ini.");
        }

        _context.Units.Remove(unit);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ToggleUnitStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (unit == null)
        {
            return Result.Failure("Satuan tidak ditemukan.");
        }

        unit.IsActive = !unit.IsActive;
        unit.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

