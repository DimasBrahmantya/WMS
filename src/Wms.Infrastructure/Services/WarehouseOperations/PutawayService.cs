using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.WarehouseOperations;

namespace Wms.Infrastructure.Services.WarehouseOperations;

public class PutawayService : IPutawayService
{
    private readonly IWmsDbContext _context;

    public PutawayService(IWmsDbContext context)
    {
        _context = context;
    }

    public async Task<PutawaySuggestionDto?> SuggestPutawayLocationAsync(
        Guid productId,
        Guid warehouseId,
        string? batchNumber = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Check if same product is already stored in this warehouse
        var existingBalance = await _context.InventoryBalances
            .Include(b => b.WarehouseLocation)
            .Where(b => b.WarehouseId == warehouseId &&
                        b.ProductId == productId &&
                        b.WarehouseLocation != null &&
                        b.WarehouseLocation.IsActive &&
                        b.OnHand > 0)
            .OrderByDescending(b => b.BatchNumber == batchNumber) // Prioritize matching batch if provided
            .ThenByDescending(b => b.OnHand)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingBalance?.WarehouseLocation != null)
        {
            return new PutawaySuggestionDto
            {
                WarehouseLocationId = existingBalance.WarehouseLocationId,
                LocationCode = existingBalance.WarehouseLocation.Code,
                LocationName = existingBalance.WarehouseLocation.Name,
                Zone = existingBalance.WarehouseLocation.Zone,
                Strategy = "SameProductExistingLocation",
                Reason = $"Lokasi ini sudah menyimpan barang yang sama (Saldo saat ini: {existingBalance.OnHand})."
            };
        }

        // 2. Check if products of the same Category are placed in a specific Zone
        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);
        if (product != null)
        {
            var sameCategoryBalance = await _context.InventoryBalances
                .Include(b => b.WarehouseLocation)
                .Include(b => b.Product)
                .Where(b => b.WarehouseId == warehouseId &&
                            b.Product != null &&
                            b.Product.CategoryId == product.CategoryId &&
                            b.WarehouseLocation != null &&
                            b.WarehouseLocation.IsActive &&
                            b.OnHand > 0)
                .FirstOrDefaultAsync(cancellationToken);

            if (sameCategoryBalance?.WarehouseLocation != null)
            {
                return new PutawaySuggestionDto
                {
                    WarehouseLocationId = sameCategoryBalance.WarehouseLocationId,
                    LocationCode = sameCategoryBalance.WarehouseLocation.Code,
                    LocationName = sameCategoryBalance.WarehouseLocation.Name,
                    Zone = sameCategoryBalance.WarehouseLocation.Zone,
                    Strategy = "SameCategoryZone",
                    Reason = $"Zona '{sameCategoryBalance.WarehouseLocation.Zone ?? "Kategori"}' menyimpan barang sejenis dalam kategori yang sama."
                };
            }
        }

        // 3. Find an active empty location/bin in the warehouse
        var occupiedLocationIds = await _context.InventoryBalances
            .Where(b => b.WarehouseId == warehouseId && b.OnHand > 0)
            .Select(b => b.WarehouseLocationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var emptyLocation = await _context.WarehouseLocations
            .Where(l => l.WarehouseId == warehouseId && l.IsActive && !occupiedLocationIds.Contains(l.Id))
            .OrderBy(l => l.Zone)
            .ThenBy(l => l.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (emptyLocation != null)
        {
            return new PutawaySuggestionDto
            {
                WarehouseLocationId = emptyLocation.Id,
                LocationCode = emptyLocation.Code,
                LocationName = emptyLocation.Name,
                Zone = emptyLocation.Zone,
                Strategy = "EmptyBin",
                Reason = "Lokasi rak kosong yang tersedia di gudang ini."
            };
        }

        // 4. Default: fallback to first active location
        var fallbackLocation = await _context.WarehouseLocations
            .Where(l => l.WarehouseId == warehouseId && l.IsActive)
            .OrderBy(l => l.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (fallbackLocation != null)
        {
            return new PutawaySuggestionDto
            {
                WarehouseLocationId = fallbackLocation.Id,
                LocationCode = fallbackLocation.Code,
                LocationName = fallbackLocation.Name,
                Zone = fallbackLocation.Zone,
                Strategy = "DefaultLocation",
                Reason = "Lokasi default aktif di gudang."
            };
        }

        return null;
    }
}

