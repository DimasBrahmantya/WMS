using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Domain.Entities.Master;

namespace Wms.Application.Features.Master.Categories;

public class CategoryService : ICategoryService
{
    private readonly IWmsDbContext _context;
    private readonly IValidator<CreateCategoryCommand> _createValidator;
    private readonly IValidator<UpdateCategoryCommand> _updateValidator;

    public CategoryService(
        IWmsDbContext context,
        IValidator<CreateCategoryCommand> createValidator,
        IValidator<UpdateCategoryCommand> updateValidator)
    {
        _context = context;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PaginatedList<CategoryDto>> GetCategoriesAsync(GetCategoriesQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.Categories
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(c => c.Name.ToLower().Contains(search) || (c.Description != null && c.Description.ToLower().Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.IsActive == query.IsActive.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        dbQuery = (query.SortBy?.ToLower(), query.SortDescending) switch
        {
            ("name", true) => dbQuery.OrderByDescending(c => c.Name),
            ("name", false) => dbQuery.OrderBy(c => c.Name),
            ("isactive", true) => dbQuery.OrderByDescending(c => c.IsActive),
            ("isactive", false) => dbQuery.OrderBy(c => c.IsActive),
            ("createdatutc", true) => dbQuery.OrderByDescending(c => c.CreatedAtUtc),
            ("createdatutc", false) => dbQuery.OrderBy(c => c.CreatedAtUtc),
            _ => dbQuery.OrderBy(c => c.Name)
        };

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.IsActive,
                c.Products.Count,
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PaginatedList<CategoryDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.IsActive,
                c.Products.Count,
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<CategoryDto>> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.IsActive,
                c.Products.Count,
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (category == null)
        {
            return Result<CategoryDto>.Failure("Kategori tidak ditemukan.");
        }

        return Result<CategoryDto>.Success(category);
    }

    public async Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CategoryDto>.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var normalizedName = command.Name.Trim();
        var exists = await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (exists)
        {
            return Result<CategoryDto>.Failure($"Kategori dengan nama '{normalizedName}' sudah ada.");
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = command.Description?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<CategoryDto>.Success(new CategoryDto(
            category.Id,
            category.Name,
            category.Description,
            category.IsActive,
            0,
            category.CreatedAtUtc,
            category.UpdatedAtUtc));
    }

    public async Task<Result> UpdateCategoryAsync(UpdateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (category == null)
        {
            return Result.Failure("Kategori tidak ditemukan.");
        }

        var normalizedName = command.Name.Trim();
        var exists = await _context.Categories
            .AnyAsync(c => c.Id != command.Id && c.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (exists)
        {
            return Result.Failure($"Kategori dengan nama '{normalizedName}' sudah digunakan.");
        }

        category.Name = normalizedName;
        category.Description = command.Description?.Trim();
        category.IsActive = command.IsActive;
        category.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null)
        {
            return Result.Failure("Kategori tidak ditemukan.");
        }

        var isReferenced = await _context.Products.AnyAsync(p => p.CategoryId == id, cancellationToken);
        if (isReferenced)
        {
            return Result.Failure("Kategori tidak dapat dihapus permanen karena masih digunakan oleh produk. Silakan nonaktifkan status kategori ini.");
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ToggleCategoryStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null)
        {
            return Result.Failure("Kategori tidak ditemukan.");
        }

        category.IsActive = !category.IsActive;
        category.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

