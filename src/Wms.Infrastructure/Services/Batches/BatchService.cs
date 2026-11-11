using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Batches;
using Wms.Domain.Common;
using Wms.Domain.Entities.Inventory;

namespace Wms.Infrastructure.Services.Batches;

public class BatchService : IBatchService
{
    private readonly IWmsDbContext _context;
    private readonly IClock _clock;

    public BatchService(IWmsDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<PaginatedList<BatchDto>> GetBatchesPagedAsync(
        GetBatchesQuery query,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_clock.TodayJakarta);
        var nearExpiryThreshold = today.AddDays(30);

        var baseQuery = _context.Batches
            .Include(b => b.Product)
            .AsNoTracking();

        if (query.ProductId.HasValue)
        {
            baseQuery = baseQuery.Where(b => b.ProductId == query.ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            baseQuery = baseQuery.Where(b =>
                b.BatchNumber.ToLower().Contains(term) ||
                b.Product.Sku.ToLower().Contains(term) ||
                b.Product.Name.ToLower().Contains(term));
        }

        if (query.OnlyBlocked == true)
        {
            baseQuery = baseQuery.Where(b => b.IsBlocked);
        }

        if (query.OnlyExpired == true)
        {
            baseQuery = baseQuery.Where(b => b.ExpiryDate.HasValue && b.ExpiryDate.Value < today);
        }

        if (query.OnlyNearExpiry == true)
        {
            baseQuery = baseQuery.Where(b =>
                b.ExpiryDate.HasValue &&
                b.ExpiryDate.Value >= today &&
                b.ExpiryDate.Value <= nearExpiryThreshold);
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var pagedBatches = await baseQuery
            .OrderByDescending(b => b.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Fetch balances for these batches to populate on_hand and reserved
        var batchNumbers = pagedBatches.Select(b => b.BatchNumber).Distinct().ToList();
        var productIds = pagedBatches.Select(b => b.ProductId).Distinct().ToList();

        var balances = await _context.InventoryBalances
            .AsNoTracking()
            .Where(ib => productIds.Contains(ib.ProductId) &&
                         ib.BatchNumber != null &&
                         batchNumbers.Contains(ib.BatchNumber))
            .ToListAsync(cancellationToken);

        var dtos = pagedBatches.Select(b =>
        {
            var batchBalances = balances.Where(ib => ib.ProductId == b.ProductId && ib.BatchNumber == b.BatchNumber).ToList();
            var onHand = batchBalances.Sum(ib => ib.OnHand);
            var reserved = batchBalances.Sum(ib => ib.Reserved);

            int? daysToExpiry = null;
            if (b.ExpiryDate.HasValue)
            {
                daysToExpiry = b.ExpiryDate.Value.DayNumber - today.DayNumber;
            }

            var isExpired = b.ExpiryDate.HasValue && b.ExpiryDate.Value < today;
            var isNearExpiry = b.ExpiryDate.HasValue && b.ExpiryDate.Value >= today && b.ExpiryDate.Value <= nearExpiryThreshold;

            return new BatchDto
            {
                Id = b.Id,
                ProductId = b.ProductId,
                ProductSku = b.Product?.Sku ?? string.Empty,
                ProductName = b.Product?.Name ?? string.Empty,
                BatchNumber = b.BatchNumber,
                ManufactureDate = b.ManufactureDate,
                ExpiryDate = b.ExpiryDate,
                IsBlocked = b.IsBlocked,
                BlockReason = b.BlockReason,
                TotalOnHand = onHand,
                TotalReserved = reserved,
                DaysToExpiry = daysToExpiry,
                IsExpired = isExpired,
                IsNearExpiry = isNearExpiry
            };
        }).ToList();

        return new PaginatedList<BatchDto>(dtos, totalCount, query.PageNumber, query.PageSize);
    }

    public async Task<BatchDto?> GetBatchByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_clock.TodayJakarta);
        var b = await _context.Batches
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (b == null) return null;

        var balances = await _context.InventoryBalances
            .Where(ib => ib.ProductId == b.ProductId && ib.BatchNumber == b.BatchNumber)
            .ToListAsync(cancellationToken);

        int? daysToExpiry = b.ExpiryDate.HasValue ? b.ExpiryDate.Value.DayNumber - today.DayNumber : null;

        return new BatchDto
        {
            Id = b.Id,
            ProductId = b.ProductId,
            ProductSku = b.Product?.Sku ?? string.Empty,
            ProductName = b.Product?.Name ?? string.Empty,
            BatchNumber = b.BatchNumber,
            ManufactureDate = b.ManufactureDate,
            ExpiryDate = b.ExpiryDate,
            IsBlocked = b.IsBlocked,
            BlockReason = b.BlockReason,
            TotalOnHand = balances.Sum(x => x.OnHand),
            TotalReserved = balances.Sum(x => x.Reserved),
            DaysToExpiry = daysToExpiry,
            IsExpired = b.IsExpired(today),
            IsNearExpiry = b.ExpiryDate.HasValue && b.ExpiryDate.Value >= today && b.ExpiryDate.Value <= today.AddDays(30)
        };
    }

    public async Task<Result<BatchDto>> CreateOrUpdateBatchAsync(
        CreateBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.BatchNumber))
            return Result<BatchDto>.Failure("Nomor batch tidak boleh kosong.");

        var product = await _context.Products.FindAsync(new object[] { command.ProductId }, cancellationToken);
        if (product == null)
            return Result<BatchDto>.Failure("Produk tidak ditemukan.");

        var batch = await _context.Batches
            .FirstOrDefaultAsync(b => b.ProductId == command.ProductId && b.BatchNumber == command.BatchNumber.Trim(), cancellationToken);

        if (batch == null)
        {
            batch = new Batch
            {
                ProductId = command.ProductId,
                BatchNumber = command.BatchNumber.Trim(),
                ManufactureDate = command.ManufactureDate,
                ExpiryDate = command.ExpiryDate,
                IsBlocked = false
            };
            _context.Batches.Add(batch);
        }
        else
        {
            batch.ManufactureDate = command.ManufactureDate;
            batch.ExpiryDate = command.ExpiryDate;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<BatchDto>.Success(new BatchDto
        {
            Id = batch.Id,
            ProductId = batch.ProductId,
            ProductSku = product.Sku,
            ProductName = product.Name,
            BatchNumber = batch.BatchNumber,
            ManufactureDate = batch.ManufactureDate,
            ExpiryDate = batch.ExpiryDate,
            IsBlocked = batch.IsBlocked,
            BlockReason = batch.BlockReason
        });
    }

    public async Task<Result> SetBlockStatusAsync(
        UpdateBatchBlockStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var batch = await _context.Batches.FindAsync(new object[] { command.BatchId }, cancellationToken);
        if (batch == null)
            return Result.Failure("Batch tidak ditemukan.");

        batch.IsBlocked = command.IsBlocked;
        batch.BlockReason = command.IsBlocked ? command.BlockReason : null;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

