using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Stock.Reconciliation;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Stock;

public class StockReconciliationService : IStockReconciliationService
{
    private readonly IWmsDbContext _dbContext;

    public StockReconciliationService(IWmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ReconciliationReport> ReconcileAsync(
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var balancesQuery = _dbContext.InventoryBalances
            .Include(b => b.Product)
            .Include(b => b.Warehouse)
            .Include(b => b.WarehouseLocation)
            .AsNoTracking();

        if (warehouseId.HasValue)
        {
            balancesQuery = balancesQuery.Where(b => b.WarehouseId == warehouseId.Value);
        }

        var balances = await balancesQuery.ToListAsync(cancellationToken);

        // Ambil movements yang Posted
        var movementsQuery = _dbContext.StockMovements
            .Where(m => m.Status == MovementStatus.Posted)
            .AsNoTracking();

        if (warehouseId.HasValue)
        {
            movementsQuery = movementsQuery.Where(m => m.WarehouseId == warehouseId.Value);
        }

        var movements = await movementsQuery.ToListAsync(cancellationToken);

        // Kelompokkan ledger berdasarkan kunci: ProductId, WarehouseId, LocationId, Status, BatchNumber
        var ledgerGroups = movements
            .GroupBy(m => new
            {
                m.ProductId,
                m.WarehouseId,
                m.WarehouseLocationId,
                m.StockStatus,
                BatchNumber = m.BatchNumber ?? string.Empty
            })
            .ToDictionary(
                g => g.Key,
                g => g.Sum(m => m.Direction == MovementDirection.In ? m.Quantity : -m.Quantity));

        var discrepancies = new List<StockDiscrepancyDto>();

        foreach (var b in balances)
        {
            var key = new
            {
                b.ProductId,
                b.WarehouseId,
                b.WarehouseLocationId,
                StockStatus = b.Status,
                BatchNumber = b.BatchNumber ?? string.Empty
            };

            ledgerGroups.TryGetValue(key, out var ledgerQty);

            if (b.OnHand != ledgerQty)
            {
                discrepancies.Add(new StockDiscrepancyDto
                {
                    ProductId = b.ProductId,
                    ProductSku = b.Product.Sku,
                    ProductName = b.Product.Name,
                    WarehouseId = b.WarehouseId,
                    WarehouseName = b.Warehouse.Name,
                    WarehouseLocationId = b.WarehouseLocationId,
                    WarehouseLocationCode = b.WarehouseLocation.Code,
                    Status = b.Status,
                    BatchNumber = b.BatchNumber,
                    BalanceOnHand = b.OnHand,
                    LedgerOnHand = ledgerQty,
                    Reason = $"Selisih saldo fisik ({b.OnHand}) vs kalkulasi ledger ({ledgerQty})"
                });
            }
        }

        return new ReconciliationReport
        {
            RunAtUtc = DateTime.UtcNow,
            TotalBalancesChecked = balances.Count,
            DiscrepancyCount = discrepancies.Count,
            Discrepancies = discrepancies
        };
    }
}

