using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Domain.Entities.Master;

namespace Wms.Application.Features.Master.Products;

public class ProductService : IProductService
{
    private readonly IWmsDbContext _context;
    private readonly IValidator<CreateProductCommand> _createValidator;
    private readonly IValidator<UpdateProductCommand> _updateValidator;

    public ProductService(
        IWmsDbContext context,
        IValidator<CreateProductCommand> createValidator,
        IValidator<UpdateProductCommand> updateValidator)
    {
        _context = context;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PaginatedList<ProductDto>> GetProductsAsync(GetProductsQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(p =>
                p.Sku.ToLower().Contains(search) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(search)) ||
                p.Name.ToLower().Contains(search) ||
                (p.Brand != null && p.Brand.ToLower().Contains(search)));
        }

        if (query.CategoryId.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(p => p.IsActive == query.IsActive.Value);
        }

        if (query.LowStockOnly == true)
        {
            dbQuery = dbQuery.Where(p => p.MinStock > 0);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        dbQuery = (query.SortBy?.ToLower(), query.SortDescending) switch
        {
            ("sku", true) => dbQuery.OrderByDescending(p => p.Sku),
            ("sku", false) => dbQuery.OrderBy(p => p.Sku),
            ("name", true) => dbQuery.OrderByDescending(p => p.Name),
            ("name", false) => dbQuery.OrderBy(p => p.Name),
            ("category", true) => dbQuery.OrderByDescending(p => p.Category.Name),
            ("category", false) => dbQuery.OrderBy(p => p.Category.Name),
            ("brand", true) => dbQuery.OrderByDescending(p => p.Brand),
            ("brand", false) => dbQuery.OrderBy(p => p.Brand),
            ("isactive", true) => dbQuery.OrderByDescending(p => p.IsActive),
            ("isactive", false) => dbQuery.OrderBy(p => p.IsActive),
            ("createdatutc", true) => dbQuery.OrderByDescending(p => p.CreatedAtUtc),
            ("createdatutc", false) => dbQuery.OrderBy(p => p.CreatedAtUtc),
            _ => dbQuery.OrderBy(p => p.Name)
        };

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductDto(
                p.Id,
                p.Sku,
                p.Barcode,
                p.Name,
                p.Description,
                p.Brand,
                p.CategoryId,
                p.Category.Name,
                p.UnitId,
                p.Unit.Name,
                p.Unit.Abbreviation,
                p.MinStock,
                p.MaxStock,
                p.TrackingMethod,
                p.IsActive,
                p.CreatedAtUtc,
                p.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PaginatedList<ProductDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<ProductDto>> GetActiveProductsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(
                p.Id,
                p.Sku,
                p.Barcode,
                p.Name,
                p.Description,
                p.Brand,
                p.CategoryId,
                p.Category.Name,
                p.UnitId,
                p.Unit.Name,
                p.Unit.Abbreviation,
                p.MinStock,
                p.MaxStock,
                p.TrackingMethod,
                p.IsActive,
                p.CreatedAtUtc,
                p.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<ProductDto>> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .Where(p => p.Id == id)
            .Select(p => new ProductDto(
                p.Id,
                p.Sku,
                p.Barcode,
                p.Name,
                p.Description,
                p.Brand,
                p.CategoryId,
                p.Category.Name,
                p.UnitId,
                p.Unit.Name,
                p.Unit.Abbreviation,
                p.MinStock,
                p.MaxStock,
                p.TrackingMethod,
                p.IsActive,
                p.CreatedAtUtc,
                p.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (product == null)
        {
            return Result<ProductDto>.Failure("Barang tidak ditemukan.");
        }

        return Result<ProductDto>.Success(product);
    }

    public async Task<Result<ProductDto>> CreateProductAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ProductDto>.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var normalizedSku = command.Sku.Trim().ToUpperInvariant();
        var skuExists = await _context.Products.AnyAsync(p => p.Sku.ToLower() == normalizedSku.ToLower(), cancellationToken);
        if (skuExists)
        {
            return Result<ProductDto>.Failure($"Barang dengan SKU '{normalizedSku}' sudah ada.");
        }

        string? normalizedBarcode = string.IsNullOrWhiteSpace(command.Barcode) ? null : command.Barcode.Trim();
        if (!string.IsNullOrEmpty(normalizedBarcode))
        {
            var barcodeExists = await _context.Products.AnyAsync(p => p.Barcode != null && p.Barcode == normalizedBarcode, cancellationToken);
            if (barcodeExists)
            {
                return Result<ProductDto>.Failure($"Barang dengan barcode '{normalizedBarcode}' sudah ada.");
            }
        }

        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == command.CategoryId, cancellationToken);
        if (category == null)
        {
            return Result<ProductDto>.Failure("Kategori yang dipilih tidak valid.");
        }

        var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == command.UnitId, cancellationToken);
        if (unit == null)
        {
            return Result<ProductDto>.Failure("Satuan dasar yang dipilih tidak valid.");
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = normalizedSku,
            Barcode = normalizedBarcode,
            Name = command.Name.Trim(),
            Description = command.Description?.Trim(),
            Brand = command.Brand?.Trim(),
            CategoryId = command.CategoryId,
            UnitId = command.UnitId,
            MinStock = command.MinStock,
            MaxStock = command.MaxStock,
            TrackingMethod = command.TrackingMethod,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<ProductDto>.Success(new ProductDto(
            product.Id,
            product.Sku,
            product.Barcode,
            product.Name,
            product.Description,
            product.Brand,
            category.Id,
            category.Name,
            unit.Id,
            unit.Name,
            unit.Abbreviation,
            product.MinStock,
            product.MaxStock,
            product.TrackingMethod,
            product.IsActive,
            product.CreatedAtUtc,
            product.UpdatedAtUtc));
    }

    public async Task<Result> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (product == null)
        {
            return Result.Failure("Barang tidak ditemukan.");
        }

        var normalizedSku = command.Sku.Trim().ToUpperInvariant();
        var skuExists = await _context.Products.AnyAsync(p => p.Id != command.Id && p.Sku.ToLower() == normalizedSku.ToLower(), cancellationToken);
        if (skuExists)
        {
            return Result.Failure($"Barang dengan SKU '{normalizedSku}' sudah digunakan.");
        }

        string? normalizedBarcode = string.IsNullOrWhiteSpace(command.Barcode) ? null : command.Barcode.Trim();
        if (!string.IsNullOrEmpty(normalizedBarcode))
        {
            var barcodeExists = await _context.Products.AnyAsync(p => p.Id != command.Id && p.Barcode != null && p.Barcode == normalizedBarcode, cancellationToken);
            if (barcodeExists)
            {
                return Result.Failure($"Barang dengan barcode '{normalizedBarcode}' sudah digunakan.");
            }
        }

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == command.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            return Result.Failure("Kategori yang dipilih tidak valid.");
        }

        var unitExists = await _context.Units.AnyAsync(u => u.Id == command.UnitId, cancellationToken);
        if (!unitExists)
        {
            return Result.Failure("Satuan dasar yang dipilih tidak valid.");
        }

        product.Sku = normalizedSku;
        product.Barcode = normalizedBarcode;
        product.Name = command.Name.Trim();
        product.Description = command.Description?.Trim();
        product.Brand = command.Brand?.Trim();
        product.CategoryId = command.CategoryId;
        product.UnitId = command.UnitId;
        product.MinStock = command.MinStock;
        product.MaxStock = command.MaxStock;
        product.TrackingMethod = command.TrackingMethod;
        product.IsActive = command.IsActive;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null)
        {
            return Result.Failure("Barang tidak ditemukan.");
        }

        // In subsequent phases, check stock_movements and inventory_balances
        // If has transactions, return failure to prevent permanent deletion
        _context.Products.Remove(product);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ToggleProductStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null)
        {
            return Result.Failure("Barang tidak ditemukan.");
        }

        product.IsActive = !product.IsActive;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

