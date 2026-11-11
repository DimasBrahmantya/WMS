using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Batches;
using Wms.Domain.Common;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Batches;

public class FefoService : IFefoService
{
    private readonly IWmsDbContext _context;
    private readonly IClock _clock;

    public FefoService(IWmsDbContext context, IClock _clock)
    {
        _context = context;
        this._clock = _clock;
    }

    public async Task<IReadOnlyList<BatchAllocationResultDto>> AllocateFefoAsync(
        Guid productId,
        Guid warehouseId,
        decimal requiredQuantity,
        bool includeReserved = false,
        CancellationToken cancellationToken = default)
    {
        if (requiredQuantity <= 0) return Array.Empty<BatchAllocationResultDto>();

        var today = DateOnly.FromDateTime(_clock.TodayJakarta);

        // Fetch balances with positive available stock
        var balances = await _context.InventoryBalances
            .Include(b => b.WarehouseLocation)
            .Where(b => b.ProductId == productId
                        && b.WarehouseId == warehouseId
                        && b.Status == StockStatus.Available
                        && (includeReserved ? b.OnHand > 0 : (b.OnHand - b.Reserved) > 0))
            .ToListAsync(cancellationToken);

        // Fetch batch metadata for this product
        var batches = await _context.Batches
            .Where(b => b.ProductId == productId)
            .ToDictionaryAsync(b => b.BatchNumber, cancellationToken);

        // Filter and sort by FEFO: earliest expiry first, non-expiring last
        var eligibleBalances = balances
            .Where(b =>
            {
                if (string.IsNullOrWhiteSpace(b.BatchNumber)) return true;
                if (batches.TryGetValue(b.BatchNumber, out var batch))
                {
                    return batch.CanBeIssued(today);
                }
                return true;
            })
            .OrderBy(b =>
            {
                if (!string.IsNullOrWhiteSpace(b.BatchNumber) &&
                    batches.TryGetValue(b.BatchNumber, out var batch) &&
                    batch.ExpiryDate.HasValue)
                {
                    return batch.ExpiryDate.Value;
                }
                return DateOnly.MaxValue;
            })
            .ThenBy(b => b.CreatedAtUtc)
            .ToList();

        var allocations = new List<BatchAllocationResultDto>();
        var remainingNeed = requiredQuantity;

        foreach (var balance in eligibleBalances)
        {
            if (remainingNeed <= 0) break;

            var available = includeReserved ? balance.OnHand : (balance.OnHand - balance.Reserved);
            var allocateQty = Math.Min(available, remainingNeed);

            DateOnly? expiry = null;
            if (!string.IsNullOrWhiteSpace(balance.BatchNumber) &&
                batches.TryGetValue(balance.BatchNumber, out var batch))
            {
                expiry = batch.ExpiryDate;
            }

            allocations.Add(new BatchAllocationResultDto
            {
                BatchNumber = balance.BatchNumber ?? string.Empty,
                WarehouseLocationId = balance.WarehouseLocationId,
                LocationCode = balance.WarehouseLocation?.Code ?? "UNKNOWN",
                AllocatedQuantity = allocateQty,
                ExpiryDate = expiry
            });

            remainingNeed -= allocateQty;
        }

        return allocations;
    }

    public async Task<bool> IsBatchExpiredOrBlockedAsync(
        Guid productId,
        string? batchNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(batchNumber)) return false;

        var batch = await _context.Batches
            .FirstOrDefaultAsync(b => b.ProductId == productId && b.BatchNumber == batchNumber, cancellationToken);

        if (batch == null) return false;

        var today = DateOnly.FromDateTime(_clock.TodayJakarta);
        return !batch.CanBeIssued(today);
    }
}

