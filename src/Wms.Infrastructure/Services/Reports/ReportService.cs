using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Dashboard;
using Wms.Application.Features.Reports;
using Wms.Domain.Common;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Reports;

public class ReportService : IReportService
{
    private readonly IWmsDbContext _dbContext;
    private readonly IUserContext _userContext;
    private readonly IClock _clock;

    public ReportService(
        IWmsDbContext dbContext,
        IUserContext userContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task<PaginatedList<StockReportItemDto>> GetCurrentStockReportAsync(
        StockReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = BuildCurrentStockQuery(query);

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        // Sorting
        dbQuery = query.SortBy?.ToLowerInvariant() switch
        {
            "sku" => query.SortDescending ? dbQuery.OrderByDescending(x => x.Product.Sku) : dbQuery.OrderBy(x => x.Product.Sku),
            "productname" => query.SortDescending ? dbQuery.OrderByDescending(x => x.Product.Name) : dbQuery.OrderBy(x => x.Product.Name),
            "warehouse" => query.SortDescending ? dbQuery.OrderByDescending(x => x.Warehouse.Name) : dbQuery.OrderBy(x => x.Warehouse.Name),
            "category" => query.SortDescending ? dbQuery.OrderByDescending(x => x.Product.Category.Name) : dbQuery.OrderBy(x => x.Product.Category.Name),
            "onhand" => query.SortDescending ? dbQuery.OrderByDescending(x => x.OnHand) : dbQuery.OrderBy(x => x.OnHand),
            "available" => query.SortDescending ? dbQuery.OrderByDescending(x => x.OnHand - x.Reserved) : dbQuery.OrderBy(x => x.OnHand - x.Reserved),
            _ => dbQuery.OrderBy(x => x.Product.Sku).ThenBy(x => x.Warehouse.Name)
        };

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(b => new StockReportItemDto
            {
                ProductId = b.ProductId,
                Sku = b.Product.Sku,
                Barcode = b.Product.Barcode,
                ProductName = b.Product.Name,
                CategoryName = b.Product.Category.Name,
                UnitName = b.Product.Unit.Name,
                WarehouseId = b.WarehouseId,
                WarehouseCode = b.Warehouse.Code,
                WarehouseName = b.Warehouse.Name,
                WarehouseLocationId = b.WarehouseLocationId,
                LocationCode = b.WarehouseLocation.Code,
                BatchNumber = b.BatchNumber,
                OnHand = b.OnHand,
                Reserved = b.Reserved,
                MinStock = b.Product.MinStock,
                MaxStock = b.Product.MaxStock
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<StockReportItemDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<StockReportSummaryDto> GetStockReportSummaryAsync(
        StockReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = BuildCurrentStockQuery(query);

        var totalSkuCount = await dbQuery
            .Select(b => b.ProductId)
            .Distinct()
            .CountAsync(cancellationToken);

        var aggregates = await dbQuery
            .GroupBy(b => new { b.ProductId, b.Product.MinStock, UnitName = b.Product.Unit.Name })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.MinStock,
                g.Key.UnitName,
                OnHand = g.Sum(x => x.OnHand)
            })
            .ToListAsync(cancellationToken);

        var lowStockCount = aggregates.Count(x => x.OnHand > 0 && x.OnHand <= x.MinStock);
        var outOfStockCount = aggregates.Count(x => x.OnHand <= 0);

        var unitTotals = aggregates
            .GroupBy(x => x.UnitName)
            .Select(g => new UnitQuantityDto
            {
                UnitName = g.Key,
                TotalQuantity = g.Sum(x => x.OnHand)
            })
            .OrderByDescending(u => u.TotalQuantity)
            .ToList();

        return new StockReportSummaryDto
        {
            TotalSkuCount = totalSkuCount,
            TotalLowStockCount = lowStockCount,
            TotalOutOfStockCount = outOfStockCount,
            UnitTotals = unitTotals,
            GeneratedAtUtc = _clock.UtcNow
        };
    }

    private IQueryable<Domain.Entities.Inventory.InventoryBalance> BuildCurrentStockQuery(StockReportQuery query)
    {
        var dbQuery = _dbContext.InventoryBalances
            .Include(b => b.Product).ThenInclude(p => p.Category)
            .Include(b => b.Product).ThenInclude(p => p.Unit)
            .Include(b => b.Warehouse)
            .Include(b => b.WarehouseLocation)
            .AsNoTracking();

        // Warehouse access control
        var allowedWarehouseIds = _userContext.AllowedWarehouseIds;
        if (allowedWarehouseIds.Count > 0)
        {
            dbQuery = dbQuery.Where(b => allowedWarehouseIds.Contains(b.WarehouseId));
        }

        if (query.WarehouseId.HasValue)
        {
            dbQuery = dbQuery.Where(b => b.WarehouseId == query.WarehouseId.Value);
        }

        if (query.CategoryId.HasValue)
        {
            dbQuery = dbQuery.Where(b => b.Product.CategoryId == query.CategoryId.Value);
        }

        if (query.ProductId.HasValue)
        {
            dbQuery = dbQuery.Where(b => b.ProductId == query.ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(b =>
                b.Product.Sku.ToLower().Contains(search) ||
                (b.Product.Barcode != null && b.Product.Barcode.ToLower().Contains(search)) ||
                b.Product.Name.ToLower().Contains(search) ||
                b.WarehouseLocation.Code.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.StockStatus) && query.StockStatus != "all")
        {
            dbQuery = query.StockStatus.ToLowerInvariant() switch
            {
                "safe" => dbQuery.Where(b => b.OnHand > b.Product.MinStock),
                "low" => dbQuery.Where(b => b.OnHand > 0 && b.OnHand <= b.Product.MinStock),
                "out" => dbQuery.Where(b => b.OnHand <= 0),
                _ => dbQuery
            };
        }

        return dbQuery;
    }

    public async Task<Result<StockCardReportDto>> GetStockCardReportAsync(
        StockCardQuery query,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.ProductId, cancellationToken);

        if (product == null)
        {
            return Result<StockCardReportDto>.Failure("Barang tidak ditemukan.");
        }

        // Warehouse access restriction
        var allowedWarehouseIds = _userContext.AllowedWarehouseIds;
        if (allowedWarehouseIds.Count > 0 && query.WarehouseId.HasValue && !allowedWarehouseIds.Contains(query.WarehouseId.Value))
        {
            return Result<StockCardReportDto>.Failure("Anda tidak memiliki akses ke gudang yang dipilih.");
        }

        string? warehouseName = null;
        if (query.WarehouseId.HasValue)
        {
            warehouseName = await _dbContext.Warehouses
                .Where(w => w.Id == query.WarehouseId.Value)
                .Select(w => w.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Date conversions (Jakarta to UTC)
        var todayJakarta = JakartaTime.Today;
        var startDateUtc = query.StartDate.HasValue
            ? JakartaTime.ToUtcStartOfDay(query.StartDate.Value)
            : JakartaTime.ToUtcStartOfDay(new DateTime(todayJakarta.Year, todayJakarta.Month, 1)); // Default awal bulan ini

        var endDateUtc = query.EndDate.HasValue
            ? JakartaTime.ToUtcEndOfDay(query.EndDate.Value)
            : JakartaTime.ToUtcEndOfDay(todayJakarta);

        // 1. Hitung Saldo Awal (Opening Balance) dari ledger sebelum startDateUtc
        var priorMovementsQuery = _dbContext.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == query.ProductId && m.Status == MovementStatus.Posted && m.TimestampUtc < startDateUtc);

        if (query.WarehouseId.HasValue)
        {
            priorMovementsQuery = priorMovementsQuery.Where(m => m.WarehouseId == query.WarehouseId.Value);
        }
        else if (allowedWarehouseIds.Count > 0)
        {
            priorMovementsQuery = priorMovementsQuery.Where(m => allowedWarehouseIds.Contains(m.WarehouseId));
        }

        var priorIn = await priorMovementsQuery
            .Where(m => m.Direction == MovementDirection.In)
            .SumAsync(m => (decimal?)m.Quantity, cancellationToken) ?? 0m;

        var priorOut = await priorMovementsQuery
            .Where(m => m.Direction == MovementDirection.Out)
            .SumAsync(m => (decimal?)m.Quantity, cancellationToken) ?? 0m;

        var openingBalance = priorIn - priorOut;

        // 2. Ambil seluruh pergerakan dalam periode filter
        var periodMovementsQuery = _dbContext.StockMovements
            .Include(m => m.Warehouse)
            .Include(m => m.WarehouseLocation)
            .Include(m => m.User)
            .AsNoTracking()
            .Where(m => m.ProductId == query.ProductId && m.TimestampUtc >= startDateUtc && m.TimestampUtc <= endDateUtc);

        if (query.WarehouseId.HasValue)
        {
            periodMovementsQuery = periodMovementsQuery.Where(m => m.WarehouseId == query.WarehouseId.Value);
        }
        else if (allowedWarehouseIds.Count > 0)
        {
            periodMovementsQuery = periodMovementsQuery.Where(m => allowedWarehouseIds.Contains(m.WarehouseId));
        }

        if (query.MovementType.HasValue)
        {
            periodMovementsQuery = periodMovementsQuery.Where(m => m.MovementType == query.MovementType.Value);
        }

        if (query.Direction.HasValue)
        {
            periodMovementsQuery = periodMovementsQuery.Where(m => m.Direction == query.Direction.Value);
        }

        var rawMovements = await periodMovementsQuery
            .OrderBy(m => m.TimestampUtc)
            .ThenBy(m => m.MovementNumber)
            .ToListAsync(cancellationToken);

        // 3. Rekonstruksi Saldo Berjalan (Running Balance)
        var runningBalance = openingBalance;
        var totalQtyIn = 0m;
        var totalQtyOut = 0m;
        var lines = new List<StockCardLineDto>();

        foreach (var m in rawMovements)
        {
            var qtyIn = m.Direction == MovementDirection.In ? m.Quantity : 0m;
            var qtyOut = m.Direction == MovementDirection.Out ? m.Quantity : 0m;

            if (m.Status == MovementStatus.Posted)
            {
                totalQtyIn += qtyIn;
                totalQtyOut += qtyOut;
                runningBalance += (qtyIn - qtyOut);
            }

            lines.Add(new StockCardLineDto
            {
                MovementId = m.Id,
                MovementNumber = m.MovementNumber,
                TimestampUtc = m.TimestampUtc,
                SourceDocumentNumber = m.SourceDocumentNumber,
                SourceDocumentType = m.SourceDocumentType,
                MovementType = m.MovementType,
                Direction = m.Direction,
                QuantityIn = qtyIn,
                QuantityOut = qtyOut,
                RunningBalance = runningBalance,
                UnitCost = m.UnitCost,
                TotalValue = m.TotalValue,
                WarehouseName = m.Warehouse.Name,
                LocationCode = m.WarehouseLocation.Code,
                Reason = m.Reason,
                OperatorName = m.User.FullName ?? m.User.UserName ?? "Sistem",
                Status = m.Status
            });
        }

        // 4. Ambil Saldo Real-Time Saat Ini dari inventory_balances untuk perbandingan transparan
        var realTimeBalanceQuery = _dbContext.InventoryBalances
            .AsNoTracking()
            .Where(b => b.ProductId == query.ProductId);

        if (query.WarehouseId.HasValue)
        {
            realTimeBalanceQuery = realTimeBalanceQuery.Where(b => b.WarehouseId == query.WarehouseId.Value);
        }
        else if (allowedWarehouseIds.Count > 0)
        {
            realTimeBalanceQuery = realTimeBalanceQuery.Where(b => allowedWarehouseIds.Contains(b.WarehouseId));
        }

        var currentRealTimeBalance = await realTimeBalanceQuery
            .SumAsync(b => (decimal?)b.OnHand, cancellationToken) ?? 0m;

        var report = new StockCardReportDto
        {
            ProductId = product.Id,
            Sku = product.Sku,
            Barcode = product.Barcode,
            ProductName = product.Name,
            CategoryName = product.Category.Name,
            UnitName = product.Unit.Name,
            MinStock = product.MinStock,
            WarehouseId = query.WarehouseId,
            WarehouseName = warehouseName,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            OpeningBalance = openingBalance,
            TotalQuantityIn = totalQtyIn,
            TotalQuantityOut = totalQtyOut,
            ClosingBalance = runningBalance,
            CurrentRealTimeBalance = currentRealTimeBalance,
            Lines = lines
        };

        return Result<StockCardReportDto>.Success(report);
    }

    public async Task<PaginatedList<ValuationReportItemDto>> GetValuationReportAsync(
        ValuationReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var allowedWarehouseIds = _userContext.AllowedWarehouseIds;

        if (query.Mode == ValuationReportMode.Current)
        {
            // Mode 1: Saldo Valuasi Real-Time dari inventory_valuations
            var dbQuery = _dbContext.InventoryValuations
                .Include(v => v.Product).ThenInclude(p => p.Category)
                .Include(v => v.Product).ThenInclude(p => p.Unit)
                .Include(v => v.Warehouse)
                .AsNoTracking();

            if (allowedWarehouseIds.Count > 0)
            {
                dbQuery = dbQuery.Where(v => allowedWarehouseIds.Contains(v.WarehouseId));
            }

            if (query.WarehouseId.HasValue)
            {
                dbQuery = dbQuery.Where(v => v.WarehouseId == query.WarehouseId.Value);
            }

            if (query.CategoryId.HasValue)
            {
                dbQuery = dbQuery.Where(v => v.Product.CategoryId == query.CategoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim().ToLower();
                dbQuery = dbQuery.Where(v =>
                    v.Product.Sku.ToLower().Contains(search) ||
                    (v.Product.Barcode != null && v.Product.Barcode.ToLower().Contains(search)) ||
                    v.Product.Name.ToLower().Contains(search) ||
                    v.Warehouse.Name.ToLower().Contains(search));
            }

            var totalCount = await dbQuery.CountAsync(cancellationToken);

            // Sorting
            dbQuery = query.SortBy?.ToLowerInvariant() switch
            {
                "sku" => query.SortDescending ? dbQuery.OrderByDescending(x => x.Product.Sku) : dbQuery.OrderBy(x => x.Product.Sku),
                "productname" => query.SortDescending ? dbQuery.OrderByDescending(x => x.Product.Name) : dbQuery.OrderBy(x => x.Product.Name),
                "onhandvalue" => query.SortDescending ? dbQuery.OrderByDescending(x => x.OnHandValue) : dbQuery.OrderBy(x => x.OnHandValue),
                "avgcost" => query.SortDescending ? dbQuery.OrderByDescending(x => x.AvgCost) : dbQuery.OrderBy(x => x.AvgCost),
                "onhandqty" => query.SortDescending ? dbQuery.OrderByDescending(x => x.OnHandQty) : dbQuery.OrderBy(x => x.OnHandQty),
                _ => dbQuery.OrderByDescending(x => x.OnHandValue)
            };

            var items = await dbQuery
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(v => new ValuationReportItemDto
                {
                    ProductId = v.ProductId,
                    Sku = v.Product.Sku,
                    Barcode = v.Product.Barcode,
                    ProductName = v.Product.Name,
                    CategoryName = v.Product.Category.Name,
                    UnitName = v.Product.Unit.Name,
                    WarehouseId = v.WarehouseId,
                    WarehouseCode = v.Warehouse.Code,
                    WarehouseName = v.Warehouse.Name,
                    OnHandQty = v.OnHandQty,
                    AvgCost = v.AvgCost,
                    OnHandValue = v.OnHandValue,
                    LastCost = v.LastCost,
                    LastUpdatedAtUtc = v.UpdatedAtUtc
                })
                .ToListAsync(cancellationToken);

            return new PaginatedList<ValuationReportItemDto>(items, totalCount, query.Page, query.PageSize);
        }
        else
        {
            // Mode 2: Saldo Valuasi Historis (Direkonstruksi dari ledger hingga tanggal AsOfDate)
            var cutOffDateJakarta = query.AsOfDate ?? JakartaTime.Today;
            var cutOffUtc = JakartaTime.ToUtcEndOfDay(cutOffDateJakarta);

            var movementQuery = _dbContext.StockMovements
                .Include(m => m.Product).ThenInclude(p => p.Category)
                .Include(m => m.Product).ThenInclude(p => p.Unit)
                .Include(m => m.Warehouse)
                .AsNoTracking()
                .Where(m => m.Status == MovementStatus.Posted && m.TimestampUtc <= cutOffUtc);

            if (allowedWarehouseIds.Count > 0)
            {
                movementQuery = movementQuery.Where(m => allowedWarehouseIds.Contains(m.WarehouseId));
            }

            if (query.WarehouseId.HasValue)
            {
                movementQuery = movementQuery.Where(m => m.WarehouseId == query.WarehouseId.Value);
            }

            if (query.CategoryId.HasValue)
            {
                movementQuery = movementQuery.Where(m => m.Product.CategoryId == query.CategoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim().ToLower();
                movementQuery = movementQuery.Where(m =>
                    m.Product.Sku.ToLower().Contains(search) ||
                    (m.Product.Barcode != null && m.Product.Barcode.ToLower().Contains(search)) ||
                    m.Product.Name.ToLower().Contains(search) ||
                    m.Warehouse.Name.ToLower().Contains(search));
            }

            // Group by Product and Warehouse to compute historical running balance & cumulative value
            var grouped = await movementQuery
                .GroupBy(m => new
                {
                    m.ProductId,
                    m.Product.Sku,
                    m.Product.Barcode,
                    m.Product.Name,
                    CategoryName = m.Product.Category.Name,
                    UnitName = m.Product.Unit.Name,
                    m.WarehouseId,
                    WarehouseCode = m.Warehouse.Code,
                    WarehouseName = m.Warehouse.Name
                })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.Sku,
                    g.Key.Barcode,
                    ProductName = g.Key.Name,
                    g.Key.CategoryName,
                    g.Key.UnitName,
                    g.Key.WarehouseId,
                    g.Key.WarehouseCode,
                    g.Key.WarehouseName,
                    QtyIn = g.Where(x => x.Direction == MovementDirection.In).Sum(x => x.Quantity),
                    QtyOut = g.Where(x => x.Direction == MovementDirection.Out).Sum(x => x.Quantity),
                    ValIn = g.Where(x => x.Direction == MovementDirection.In).Sum(x => x.TotalValue),
                    ValOut = g.Where(x => x.Direction == MovementDirection.Out).Sum(x => x.TotalValue),
                    LastTimestamp = g.Max(x => x.TimestampUtc),
                    LastCost = g.OrderByDescending(x => x.TimestampUtc).Select(x => x.UnitCost).FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            var reconstructedItems = grouped
                .Select(g =>
                {
                    var onHandQty = g.QtyIn - g.QtyOut;
                    var onHandValue = onHandQty <= 0 ? 0m : Math.Max(0m, g.ValIn - g.ValOut);
                    var avgCost = onHandQty > 0 ? Math.Round(onHandValue / onHandQty, 4) : 0m;

                    return new ValuationReportItemDto
                    {
                        ProductId = g.ProductId,
                        Sku = g.Sku,
                        Barcode = g.Barcode,
                        ProductName = g.ProductName,
                        CategoryName = g.CategoryName,
                        UnitName = g.UnitName,
                        WarehouseId = g.WarehouseId,
                        WarehouseCode = g.WarehouseCode,
                        WarehouseName = g.WarehouseName,
                        OnHandQty = onHandQty,
                        AvgCost = avgCost,
                        OnHandValue = onHandValue,
                        LastCost = g.LastCost,
                        LastUpdatedAtUtc = g.LastTimestamp
                    };
                })
                .ToList();

            var totalCount = reconstructedItems.Count;

            // In-memory sorting for reconstructed list
            IEnumerable<ValuationReportItemDto> sortedItems = query.SortBy?.ToLowerInvariant() switch
            {
                "sku" => query.SortDescending ? reconstructedItems.OrderByDescending(x => x.Sku) : reconstructedItems.OrderBy(x => x.Sku),
                "productname" => query.SortDescending ? reconstructedItems.OrderByDescending(x => x.ProductName) : reconstructedItems.OrderBy(x => x.ProductName),
                "onhandvalue" => query.SortDescending ? reconstructedItems.OrderByDescending(x => x.OnHandValue) : reconstructedItems.OrderBy(x => x.OnHandValue),
                "avgcost" => query.SortDescending ? reconstructedItems.OrderByDescending(x => x.AvgCost) : reconstructedItems.OrderBy(x => x.AvgCost),
                "onhandqty" => query.SortDescending ? reconstructedItems.OrderByDescending(x => x.OnHandQty) : reconstructedItems.OrderBy(x => x.OnHandQty),
                _ => query.SortDescending ? reconstructedItems.OrderBy(x => x.OnHandValue) : reconstructedItems.OrderByDescending(x => x.OnHandValue)
            };

            var pagedItems = sortedItems
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return new PaginatedList<ValuationReportItemDto>(pagedItems, totalCount, query.Page, query.PageSize);
        }
    }

    public async Task<ValuationReportSummaryDto> GetValuationReportSummaryAsync(
        ValuationReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var allowedWarehouseIds = _userContext.AllowedWarehouseIds;

        if (query.Mode == ValuationReportMode.Current)
        {
            var dbQuery = _dbContext.InventoryValuations
                .Include(v => v.Warehouse)
                .Include(v => v.Product)
                .AsNoTracking();

            if (allowedWarehouseIds.Count > 0)
            {
                dbQuery = dbQuery.Where(v => allowedWarehouseIds.Contains(v.WarehouseId));
            }

            if (query.WarehouseId.HasValue)
            {
                dbQuery = dbQuery.Where(v => v.WarehouseId == query.WarehouseId.Value);
            }

            if (query.CategoryId.HasValue)
            {
                dbQuery = dbQuery.Where(v => v.Product.CategoryId == query.CategoryId.Value);
            }

            var totalValue = await dbQuery.SumAsync(v => (decimal?)v.OnHandValue, cancellationToken) ?? 0m;
            var totalItems = await dbQuery.CountAsync(v => v.OnHandQty > 0, cancellationToken);

            var warehouseBreakdown = await dbQuery
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

            return new ValuationReportSummaryDto
            {
                Mode = ValuationReportMode.Current,
                CalculationDateUtc = _clock.UtcNow,
                TotalValuationValue = totalValue,
                TotalValuedItems = totalItems,
                WarehouseBreakdown = warehouseBreakdown
            };
        }
        else
        {
            // Historical calculation
            var cutOffDateJakarta = query.AsOfDate ?? JakartaTime.Today;
            var cutOffUtc = JakartaTime.ToUtcEndOfDay(cutOffDateJakarta);

            var movementQuery = _dbContext.StockMovements
                .Include(m => m.Warehouse)
                .Include(m => m.Product)
                .AsNoTracking()
                .Where(m => m.Status == MovementStatus.Posted && m.TimestampUtc <= cutOffUtc);

            if (allowedWarehouseIds.Count > 0)
            {
                movementQuery = movementQuery.Where(m => allowedWarehouseIds.Contains(m.WarehouseId));
            }

            if (query.WarehouseId.HasValue)
            {
                movementQuery = movementQuery.Where(m => m.WarehouseId == query.WarehouseId.Value);
            }

            if (query.CategoryId.HasValue)
            {
                movementQuery = movementQuery.Where(m => m.Product.CategoryId == query.CategoryId.Value);
            }

            var grouped = await movementQuery
                .GroupBy(m => new { m.WarehouseId, m.Warehouse.Code, m.Warehouse.Name, m.ProductId })
                .Select(g => new
                {
                    g.Key.WarehouseId,
                    g.Key.Code,
                    g.Key.Name,
                    g.Key.ProductId,
                    QtyIn = g.Where(x => x.Direction == MovementDirection.In).Sum(x => x.Quantity),
                    QtyOut = g.Where(x => x.Direction == MovementDirection.Out).Sum(x => x.Quantity),
                    ValIn = g.Where(x => x.Direction == MovementDirection.In).Sum(x => x.TotalValue),
                    ValOut = g.Where(x => x.Direction == MovementDirection.Out).Sum(x => x.TotalValue)
                })
                .ToListAsync(cancellationToken);

            var warehouseDict = new Dictionary<Guid, (string Code, string Name, decimal Value, decimal Qty)>();
            var valuedItemsCount = 0;
            var totalValue = 0m;

            foreach (var item in grouped)
            {
                var onHand = item.QtyIn - item.QtyOut;
                if (onHand > 0)
                {
                    valuedItemsCount++;
                    var val = Math.Max(0m, item.ValIn - item.ValOut);
                    totalValue += val;

                    if (!warehouseDict.TryGetValue(item.WarehouseId, out var wh))
                    {
                        warehouseDict[item.WarehouseId] = (item.Code, item.Name, val, onHand);
                    }
                    else
                    {
                        warehouseDict[item.WarehouseId] = (wh.Code, wh.Name, wh.Value + val, wh.Qty + onHand);
                    }
                }
            }

            var warehouseBreakdown = warehouseDict
                .Select(kvp => new WarehouseValuationDto
                {
                    WarehouseId = kvp.Key,
                    WarehouseCode = kvp.Value.Code,
                    WarehouseName = kvp.Value.Name,
                    TotalValue = kvp.Value.Value,
                    TotalQuantity = kvp.Value.Qty
                })
                .OrderByDescending(w => w.TotalValue)
                .ToList();

            return new ValuationReportSummaryDto
            {
                Mode = ValuationReportMode.Historical,
                CalculationDateUtc = cutOffUtc,
                TotalValuationValue = totalValue,
                TotalValuedItems = valuedItemsCount,
                WarehouseBreakdown = warehouseBreakdown
            };
        }
    }
}
