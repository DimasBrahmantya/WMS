using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Dashboard;
using Wms.Domain.Common;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Dashboard;

public class DashboardService : IDashboardService
{
    private readonly IWmsDbContext _dbContext;
    private readonly IUserContext _userContext;
    private readonly IClock _clock;

    public DashboardService(
        IWmsDbContext dbContext,
        IUserContext userContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(
        DashboardFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.UtcNow;
        var todayJakarta = JakartaTime.Today;
        var todayStartUtc = JakartaTime.ToUtcStartOfDay(todayJakarta);
        var todayEndUtc = JakartaTime.ToUtcEndOfDay(todayJakarta);

        // Date range filters
        var startDateUtc = filter.StartDate.HasValue
            ? JakartaTime.ToUtcStartOfDay(filter.StartDate.Value)
            : JakartaTime.ToUtcStartOfDay(todayJakarta.AddDays(-29)); // Default 30 hari terakhir

        var endDateUtc = filter.EndDate.HasValue
            ? JakartaTime.ToUtcEndOfDay(filter.EndDate.Value)
            : todayEndUtc;

        // Warehouse restriction
        var allowedWarehouseIds = _userContext.AllowedWarehouseIds;
        var hasWarehouseRestriction = allowedWarehouseIds.Count > 0;

        Guid? targetWarehouseId = filter.WarehouseId;
        if (targetWarehouseId.HasValue && hasWarehouseRestriction && !allowedWarehouseIds.Contains(targetWarehouseId.Value))
        {
            // Fallback to first allowed warehouse if user requests unauthorized warehouse
            targetWarehouseId = allowedWarehouseIds.FirstOrDefault();
        }

        string? targetWarehouseName = null;
        if (targetWarehouseId.HasValue)
        {
            targetWarehouseName = await _dbContext.Warehouses
                .Where(w => w.Id == targetWarehouseId.Value)
                .Select(w => w.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // 1. Total SKU Aktif
        var totalActiveSku = await _dbContext.Products
            .CountAsync(p => p.IsActive, cancellationToken);

        // 2. Total Gudang Aktif
        var warehouseQuery = _dbContext.Warehouses.AsNoTracking().Where(w => w.IsActive);
        if (hasWarehouseRestriction)
        {
            warehouseQuery = warehouseQuery.Where(w => allowedWarehouseIds.Contains(w.Id));
        }
        var totalActiveWarehouses = await warehouseQuery.CountAsync(cancellationToken);

        // 3. Valuasi Persediaan (Moving Average)
        var valQuery = _dbContext.InventoryValuations.AsNoTracking();
        if (targetWarehouseId.HasValue)
        {
            valQuery = valQuery.Where(v => v.WarehouseId == targetWarehouseId.Value);
        }
        else if (hasWarehouseRestriction)
        {
            valQuery = valQuery.Where(v => allowedWarehouseIds.Contains(v.WarehouseId));
        }

        var totalValuation = await valQuery.SumAsync(v => (decimal?)v.OnHandValue, cancellationToken) ?? 0m;

        // Breakdown Valuasi per Gudang
        var valuationByWarehouse = await valQuery
            .GroupBy(v => new { v.WarehouseId, v.Warehouse.Code, v.Warehouse.Name })
            .Select(g => new WarehouseValuationDto
            {
                WarehouseId = g.Key.WarehouseId,
                WarehouseCode = g.Key.Code,
                WarehouseName = g.Key.Name,
                TotalValue = g.Sum(x => x.OnHandValue),
                TotalQuantity = g.Sum(x => x.OnHandQty)
            })
            .OrderByDescending(w => w.TotalValue)
            .ToListAsync(cancellationToken);

        // 4. Kuantitas per Satuan (jangan menjumlahkan satuan berbeda)
        var balanceQuery = _dbContext.InventoryBalances.AsNoTracking();
        if (targetWarehouseId.HasValue)
        {
            balanceQuery = balanceQuery.Where(b => b.WarehouseId == targetWarehouseId.Value);
        }
        else if (hasWarehouseRestriction)
        {
            balanceQuery = balanceQuery.Where(b => allowedWarehouseIds.Contains(b.WarehouseId));
        }

        var quantitiesByUnit = await balanceQuery
            .GroupBy(b => b.Product.Unit.Name)
            .Select(g => new UnitQuantityDto
            {
                UnitName = g.Key,
                TotalQuantity = g.Sum(x => x.OnHand)
            })
            .OrderByDescending(u => u.TotalQuantity)
            .ToListAsync(cancellationToken);

        // 5. Barang Stok Rendah dan Habis
        // Menggabungkan saldo on hand per produk pada cakupan gudang terpilih
        var productStockAggregates = await balanceQuery
            .GroupBy(b => new { b.ProductId, b.Product.MinStock })
            .Select(g => new
            {
                ProductId = g.Key.ProductId,
                MinStock = g.Key.MinStock,
                TotalOnHand = g.Sum(x => x.OnHand)
            })
            .ToListAsync(cancellationToken);

        var lowStockCount = productStockAggregates.Count(p => p.TotalOnHand > 0 && p.TotalOnHand <= p.MinStock);
        var outOfStockCount = productStockAggregates.Count(p => p.TotalOnHand <= 0);

        // Also check active products that have no balance records at all (0 stock)
        var productsWithBalanceIds = productStockAggregates.Select(p => p.ProductId).ToHashSet();
        var zeroStockProductsCount = await _dbContext.Products
            .CountAsync(p => p.IsActive && !productsWithBalanceIds.Contains(p.Id), cancellationToken);
        outOfStockCount += zeroStockProductsCount;

        // 6. Transaksi Hari Ini (GoodsReceipts & StockOuts)
        var grQuery = _dbContext.GoodsReceipts.AsNoTracking()
            .Where(r => r.Status == DocumentStatus.Posted && r.PostedAtUtc >= todayStartUtc && r.PostedAtUtc <= todayEndUtc);
        var soQuery = _dbContext.StockOuts.AsNoTracking()
            .Where(s => s.Status == DocumentStatus.Posted && s.PostedAtUtc >= todayStartUtc && s.PostedAtUtc <= todayEndUtc);

        if (targetWarehouseId.HasValue)
        {
            grQuery = grQuery.Where(r => r.WarehouseId == targetWarehouseId.Value);
            soQuery = soQuery.Where(s => s.WarehouseId == targetWarehouseId.Value);
        }
        else if (hasWarehouseRestriction)
        {
            grQuery = grQuery.Where(r => allowedWarehouseIds.Contains(r.WarehouseId));
            soQuery = soQuery.Where(s => allowedWarehouseIds.Contains(s.WarehouseId));
        }

        var todayGoodsReceipts = await grQuery.CountAsync(cancellationToken);
        var todayStockOuts = await soQuery.CountAsync(cancellationToken);

        // Movement query for the selected date range
        var movementQuery = _dbContext.StockMovements.AsNoTracking()
            .Where(m => m.Status == MovementStatus.Posted);

        if (targetWarehouseId.HasValue)
        {
            movementQuery = movementQuery.Where(m => m.WarehouseId == targetWarehouseId.Value);
        }
        else if (hasWarehouseRestriction)
        {
            movementQuery = movementQuery.Where(m => allowedWarehouseIds.Contains(m.WarehouseId));
        }

        var todayMovementCount = await movementQuery
            .CountAsync(m => m.TimestampUtc >= todayStartUtc && m.TimestampUtc <= todayEndUtc, cancellationToken);

        // 7. Distribusi Stok per Kategori
        var categoryDistribution = await balanceQuery
            .GroupBy(b => new { b.Product.CategoryId, b.Product.Category.Name })
            .Select(g => new CategoryStockDto
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.Name,
                ProductCount = g.Select(x => x.ProductId).Distinct().Count(),
                TotalQuantity = g.Sum(x => x.OnHand)
            })
            .OrderByDescending(c => c.TotalQuantity)
            .Take(10)
            .ToListAsync(cancellationToken);

        // 8. Grafik Tren Masuk/Keluar per Hari (dalam rentang tanggal)
        var periodMovements = await movementQuery
            .Where(m => m.TimestampUtc >= startDateUtc && m.TimestampUtc <= endDateUtc)
            .Select(m => new
            {
                m.TimestampUtc,
                m.Direction,
                m.Quantity
            })
            .ToListAsync(cancellationToken);

        var movementTrends = periodMovements
            .GroupBy(m => JakartaTime.ToJakarta(m.TimestampUtc).Date)
            .OrderBy(g => g.Key)
            .Select(g => new MovementTrendDto
            {
                Date = g.Key,
                DateLabel = g.Key.ToString("dd/MM"),
                InQty = g.Where(x => x.Direction == MovementDirection.In).Sum(x => x.Quantity),
                OutQty = g.Where(x => x.Direction == MovementDirection.Out).Sum(x => x.Quantity),
                TransactionCount = g.Count()
            })
            .ToList();

        // 9. Barang Paling Sering Keluar (Top Fast-Moving)
        var topMovingProducts = await movementQuery
            .Where(m => m.Direction == MovementDirection.Out && m.TimestampUtc >= startDateUtc && m.TimestampUtc <= endDateUtc)
            .GroupBy(m => new { m.ProductId, m.Product.Sku, m.Product.Name, CategoryName = m.Product.Category.Name, UnitName = m.Product.Unit.Name })
            .Select(g => new TopMovingProductDto
            {
                ProductId = g.Key.ProductId,
                Sku = g.Key.Sku,
                ProductName = g.Key.Name,
                CategoryName = g.Key.CategoryName,
                UnitName = g.Key.UnitName,
                TotalOutQty = g.Sum(x => x.Quantity),
                TotalOutValue = g.Sum(x => x.TotalValue)
            })
            .OrderByDescending(p => p.TotalOutQty)
            .Take(5)
            .ToListAsync(cancellationToken);

        // 10. Barang Tidak Bergerak (Dead Stock / Slow Moving)
        // Ambil barang aktif dengan saldo > 0 yang tidak ada movement keluar dalam ambang batas hari
        var deadStockThresholdUtc = nowUtc.AddDays(-Math.Max(7, filter.InactiveThresholdDays));

        var deadStockCandidates = await balanceQuery
            .Where(b => b.OnHand > 0)
            .GroupBy(b => new
            {
                b.ProductId,
                b.Product.Sku,
                b.Product.Name,
                CategoryName = b.Product.Category.Name,
                UnitName = b.Product.Unit.Name,
                WarehouseName = b.Warehouse.Name
            })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Sku,
                ProductName = g.Key.Name,
                g.Key.CategoryName,
                g.Key.UnitName,
                g.Key.WarehouseName,
                OnHand = g.Sum(x => x.OnHand)
            })
            .ToListAsync(cancellationToken);

        var candidateProductIds = deadStockCandidates.Select(c => c.ProductId).Distinct().ToList();

        // Cari pergerakan terakhir per produk
        var lastMovements = await _dbContext.StockMovements.AsNoTracking()
            .Where(m => candidateProductIds.Contains(m.ProductId) && m.Status == MovementStatus.Posted)
            .GroupBy(m => m.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                LastMovementUtc = g.Max(x => x.TimestampUtc)
            })
            .ToDictionaryAsync(x => x.ProductId, x => x.LastMovementUtc, cancellationToken);

        var deadStockList = new List<DeadStockProductDto>();
        foreach (var c in deadStockCandidates)
        {
            lastMovements.TryGetValue(c.ProductId, out var lastUtc);
            if (lastUtc == default || lastUtc <= deadStockThresholdUtc)
            {
                var days = lastUtc == default
                    ? filter.InactiveThresholdDays + 30
                    : (int)(nowUtc - lastUtc).TotalDays;

                deadStockList.Add(new DeadStockProductDto
                {
                    ProductId = c.ProductId,
                    Sku = c.Sku,
                    ProductName = c.ProductName,
                    CategoryName = c.CategoryName,
                    UnitName = c.UnitName,
                    WarehouseName = c.WarehouseName,
                    CurrentOnHand = c.OnHand,
                    LastMovementUtc = lastUtc == default ? null : lastUtc,
                    DaysInactive = days
                });
            }
        }

        // 11. Aktivitas Stok Terbaru
        var recentMovements = await movementQuery
            .OrderByDescending(m => m.TimestampUtc)
            .Take(7)
            .Select(m => new RecentMovementItemDto
            {
                Id = m.Id,
                MovementNumber = m.MovementNumber,
                SourceDocumentNumber = m.SourceDocumentNumber,
                SourceDocumentType = m.SourceDocumentType,
                ProductName = m.Product.Name,
                Sku = m.Product.Sku,
                WarehouseName = m.Warehouse.Name,
                LocationCode = m.WarehouseLocation.Code,
                Direction = m.Direction,
                MovementType = m.MovementType,
                Quantity = m.Quantity,
                UnitName = m.Product.Unit.Name,
                UnitCost = m.UnitCost,
                TotalValue = m.TotalValue,
                TimestampUtc = m.TimestampUtc,
                OperatorName = m.User.FullName ?? m.User.UserName ?? "Sistem",
                Status = m.Status
            })
            .ToListAsync(cancellationToken);

        return new DashboardSummaryDto
        {
            TotalActiveSku = totalActiveSku,
            TotalActiveWarehouses = totalActiveWarehouses,
            TotalValuation = totalValuation,
            QuantitiesByUnit = quantitiesByUnit,
            ValuationByWarehouse = valuationByWarehouse,
            LowStockCount = lowStockCount,
            OutOfStockCount = outOfStockCount,
            TodayGoodsReceiptsCount = todayGoodsReceipts,
            TodayStockOutsCount = todayStockOuts,
            TodayMovementCount = todayMovementCount,
            PendingPurchaseOrdersCount = 0, // Fase 2
            PendingTransfersCount = 0,      // Fase 2
            ReportedIncidentsCount = 0,     // Fase 2
            CategoryDistribution = categoryDistribution,
            MovementTrends = movementTrends,
            TopMovingProducts = topMovingProducts,
            DeadStockProducts = deadStockList.OrderByDescending(d => d.DaysInactive).Take(5).ToList(),
            RecentMovements = recentMovements,
            FilterWarehouseId = targetWarehouseId,
            FilterWarehouseName = targetWarehouseName,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc
        };
    }
}
