using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Audit;
using Wms.Application.Features.Batches;
using Wms.Application.Features.SalesOrders;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Domain.Common;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Orders;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.SalesOrders;

public class SalesOrderService : ISalesOrderService
{
    private readonly IWmsDbContext _context;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IFefoService _fefoService;
    private readonly IAppAuditService _auditService;
    private readonly IClock _clock;

    public SalesOrderService(
        IWmsDbContext context,
        IDocumentSequenceService sequenceService,
        IFefoService fefoService,
        IAppAuditService auditService,
        IClock clock)
    {
        _context = context;
        _sequenceService = sequenceService;
        _fefoService = fefoService;
        _auditService = auditService;
        _clock = clock;
    }

    public async Task<PaginatedList<SalesOrderDto>> GetSalesOrdersPagedAsync(
        GetSalesOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.Warehouse)
            .Include(so => so.CreatedByUser)
            .Include(so => so.ConfirmedByUser)
            .Include(so => so.DispatchedByUser)
            .Include(so => so.Lines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(p => p.Unit)
            .AsNoTracking();

        if (query.WarehouseId.HasValue)
            baseQuery = baseQuery.Where(so => so.WarehouseId == query.WarehouseId.Value);

        if (query.CustomerId.HasValue)
            baseQuery = baseQuery.Where(so => so.CustomerId == query.CustomerId.Value);

        if (query.Status.HasValue)
            baseQuery = baseQuery.Where(so => so.Status == query.Status.Value);

        if (query.StartDate.HasValue)
            baseQuery = baseQuery.Where(so => so.OrderDate >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            baseQuery = baseQuery.Where(so => so.OrderDate <= query.EndDate.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(so =>
                so.OrderNumber.ToLower().Contains(term) ||
                so.Customer.Name.ToLower().Contains(term) ||
                (so.Notes != null && so.Notes.ToLower().Contains(term)));
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var orders = await baseQuery
            .OrderByDescending(so => so.OrderDate)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = orders.Select(MapToDto).ToList();
        return new PaginatedList<SalesOrderDto>(dtos, totalCount, query.Page, query.PageSize);
    }

    public async Task<SalesOrderDto?> GetSalesOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.Warehouse)
            .Include(so => so.CreatedByUser)
            .Include(so => so.ConfirmedByUser)
            .Include(so => so.DispatchedByUser)
            .Include(so => so.Lines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(p => p.Unit)
            .AsNoTracking()
            .FirstOrDefaultAsync(so => so.Id == id, cancellationToken);

        return order == null ? null : MapToDto(order);
    }

    public async Task<Result<SalesOrderDto>> CreateSalesOrderAsync(
        CreateSalesOrderCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (command.CustomerId == Guid.Empty)
            return Result<SalesOrderDto>.Failure("Pelanggan harus dipilih.");

        if (command.WarehouseId == Guid.Empty)
            return Result<SalesOrderDto>.Failure("Gudang harus dipilih.");

        if (command.Lines.Count == 0)
            return Result<SalesOrderDto>.Failure("Sales Order harus memiliki minimal satu baris barang.");

        foreach (var line in command.Lines)
        {
            if (line.ProductId == Guid.Empty)
                return Result<SalesOrderDto>.Failure("Produk pada setiap baris harus valid.");
            if (line.OrderedQuantity <= 0)
                return Result<SalesOrderDto>.Failure("Kuantitas pemesanan harus lebih besar dari 0.");
        }

        var orderNumber = await _sequenceService.GetNextDocumentNumberAsync("SO", _clock.UtcNow, cancellationToken);

        var order = new SalesOrder
        {
            OrderNumber = orderNumber,
            CustomerId = command.CustomerId,
            WarehouseId = command.WarehouseId,
            OrderDate = command.OrderDate,
            DeliveryDate = command.DeliveryDate,
            Notes = command.Notes,
            Status = SalesOrderStatus.Draft,
            CreatedByUserId = userId,
            CreatedAtUtc = _clock.UtcNow
        };

        decimal total = 0;
        foreach (var lineCmd in command.Lines)
        {
            var line = new SalesOrderLine
            {
                ProductId = lineCmd.ProductId,
                OrderedQuantity = lineCmd.OrderedQuantity,
                ReservedQuantity = 0,
                DispatchedQuantity = 0,
                UnitPrice = lineCmd.UnitPrice,
                Notes = lineCmd.Notes
            };
            total += line.Subtotal;
            order.Lines.Add(line);
        }
        order.TotalAmount = total;

        _context.SalesOrders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            nameof(SalesOrder),
            order.Id.ToString(),
            "Create",
            userId,
            null,
            $"Created {order.OrderNumber} with {order.Lines.Count} items",
            null,
            cancellationToken);

        var resultDto = await GetSalesOrderByIdAsync(order.Id, cancellationToken);
        return Result<SalesOrderDto>.Success(resultDto!);
    }

    public async Task<Result> ConfirmSalesOrderAsync(
        Guid salesOrderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await _context.SalesOrders
                .Include(so => so.Lines)
                    .ThenInclude(l => l.Product)
                .FirstOrDefaultAsync(so => so.Id == salesOrderId, cancellationToken);

            if (order == null)
                return Result.Failure("Sales Order tidak ditemukan.");

            if (order.Status != SalesOrderStatus.Draft)
                return Result.Failure($"Sales Order tidak dapat dikonfirmasi karena status saat ini adalah '{order.Status}'.");

            // Soft Reservation: Check available stock and reserve on balances
            foreach (var line in order.Lines)
            {
                var balances = await _context.InventoryBalances
                    .Where(b => b.ProductId == line.ProductId &&
                                b.WarehouseId == order.WarehouseId &&
                                b.Status == StockStatus.Available)
                    .OrderBy(b => b.CreatedAtUtc)
                    .ToListAsync(cancellationToken);

                var totalAvailable = balances.Sum(b => b.OnHand - b.Reserved);
                if (totalAvailable < line.OrderedQuantity)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure($"Stok tersedia tidak mencukupi untuk reservasi {line.Product?.Sku ?? line.ProductId.ToString()}. Tersedia: {totalAvailable}, Dipesan: {line.OrderedQuantity}.");
                }

                // Increment Reserved across balances
                var needToReserve = line.OrderedQuantity;
                foreach (var b in balances)
                {
                    if (needToReserve <= 0) break;
                    var availableInBin = b.OnHand - b.Reserved;
                    if (availableInBin <= 0) continue;

                    var take = Math.Min(availableInBin, needToReserve);
                    b.Reserved += take;
                    b.UpdatedAtUtc = _clock.UtcNow;
                    needToReserve -= take;
                }

                line.ReservedQuantity = line.OrderedQuantity;
            }

            order.Status = SalesOrderStatus.Confirmed;
            order.ConfirmedByUserId = userId;
            order.ConfirmedAtUtc = _clock.UtcNow;
            order.UpdatedAtUtc = _clock.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                nameof(SalesOrder),
                order.Id.ToString(),
                "Confirm",
                userId,
                "Draft",
                "Confirmed with soft stock reservation",
                null,
                cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure($"Gagal mengonfirmasi Sales Order: {ex.Message}");
        }
    }

    public async Task<Result> DispatchSalesOrderAsync(
        DispatchSalesOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await _context.SalesOrders
                .Include(so => so.Lines)
                    .ThenInclude(l => l.Product)
                .FirstOrDefaultAsync(so => so.Id == command.SalesOrderId, cancellationToken);

            if (order == null)
                return Result.Failure("Sales Order tidak ditemukan.");

            if (order.Status != SalesOrderStatus.Confirmed && order.Status != SalesOrderStatus.Picking)
                return Result.Failure($"Sales Order tidak dapat dikirim karena status saat ini adalah '{order.Status}'.");

            var now = _clock.UtcNow;
            var today = DateOnly.FromDateTime(_clock.TodayJakarta);

            // If allocations were not provided by UI, auto-allocate using FEFO
            var allocations = command.Allocations;
            if (allocations == null || allocations.Count == 0)
            {
                allocations = new List<DispatchLineAllocationCommand>();
                foreach (var line in order.Lines)
                {
                    var fefoAllocations = await _fefoService.AllocateFefoAsync(
                        line.ProductId,
                        order.WarehouseId,
                        line.OrderedQuantity,
                        includeReserved: true,
                        cancellationToken: cancellationToken);

                    foreach (var fa in fefoAllocations)
                    {
                        allocations.Add(new DispatchLineAllocationCommand
                        {
                            SalesOrderLineId = line.Id,
                            WarehouseLocationId = fa.WarehouseLocationId,
                            BatchNumber = string.IsNullOrWhiteSpace(fa.BatchNumber) ? null : fa.BatchNumber,
                            Quantity = fa.AllocatedQuantity
                        });
                    }
                }
            }

            // Execute dispatch & decrement OnHand + Reserved
            foreach (var alloc in allocations)
            {
                var line = order.Lines.FirstOrDefault(l => l.Id == alloc.SalesOrderLineId);
                if (line == null) continue;

                // Validate batch not expired / blocked
                if (!string.IsNullOrWhiteSpace(alloc.BatchNumber))
                {
                    var isBlockedOrExpired = await _fefoService.IsBatchExpiredOrBlockedAsync(line.ProductId, alloc.BatchNumber, cancellationToken);
                    if (isBlockedOrExpired)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result.Failure($"Batch '{alloc.BatchNumber}' kedaluwarsa atau diblokir dari pengiriman.");
                    }
                }

                var balance = await _context.InventoryBalances
                    .FirstOrDefaultAsync(b => b.ProductId == line.ProductId &&
                                              b.WarehouseId == order.WarehouseId &&
                                              b.WarehouseLocationId == alloc.WarehouseLocationId &&
                                              b.BatchNumber == alloc.BatchNumber, cancellationToken);

                if (balance == null || balance.OnHand < alloc.Quantity)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure($"Stok fisik tidak mencukupi di lokasi yang dialokasikan untuk {line.Product?.Sku}.");
                }

                var balanceBefore = balance.OnHand;
                balance.OnHand -= alloc.Quantity;
                balance.Reserved = Math.Max(0, balance.Reserved - alloc.Quantity);
                balance.UpdatedAtUtc = now;

                line.DispatchedQuantity += alloc.Quantity;

                // Get average cost
                var valuation = await _context.InventoryValuations
                    .FirstOrDefaultAsync(v => v.ProductId == line.ProductId && v.WarehouseId == order.WarehouseId, cancellationToken);
                var unitCost = valuation?.AvgCost ?? 0;

                var movementNumber = await _sequenceService.GetNextDocumentNumberAsync("GI", now, cancellationToken);

                // Create append-only StockMovement
                var movement = new StockMovement
                {
                    MovementNumber = movementNumber,
                    MovementType = MovementType.SalesOrderIssue,
                    ProductId = line.ProductId,
                    WarehouseId = order.WarehouseId,
                    WarehouseLocationId = alloc.WarehouseLocationId,
                    BatchNumber = alloc.BatchNumber,
                    StockStatus = StockStatus.Available,
                    Quantity = alloc.Quantity,
                    Direction = MovementDirection.Out,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balance.OnHand,
                    UnitCost = unitCost,
                    TotalValue = alloc.Quantity * unitCost,
                    SourceDocumentType = SourceDocumentType.SalesOrder,
                    SourceDocumentId = order.Id,
                    SourceDocumentNumber = order.OrderNumber,
                    Reason = $"Pengiriman Sales Order {order.OrderNumber}",
                    UserId = command.UserId,
                    TimestampUtc = now,
                    Status = MovementStatus.Posted
                };

                _context.StockMovements.Add(movement);
            }

            order.Status = SalesOrderStatus.Dispatched;
            order.DispatchedByUserId = command.UserId;
            order.DispatchedAtUtc = now;
            order.UpdatedAtUtc = now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                nameof(SalesOrder),
                order.Id.ToString(),
                "Dispatch",
                command.UserId,
                "Confirmed",
                $"Dispatched {order.OrderNumber} with stock deduction",
                null,
                cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure($"Gagal memproses pengiriman Sales Order: {ex.Message}");
        }
    }

    public async Task<Result> CancelSalesOrderAsync(
        Guid salesOrderId,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await _context.SalesOrders
                .Include(so => so.Lines)
                .FirstOrDefaultAsync(so => so.Id == salesOrderId, cancellationToken);

            if (order == null)
                return Result.Failure("Sales Order tidak ditemukan.");

            if (order.Status == SalesOrderStatus.Dispatched)
                return Result.Failure("Sales Order yang sudah terkirim tidak dapat dibatalkan (gunakan retur penjualan).");

            if (order.Status == SalesOrderStatus.Cancelled)
                return Result.Failure("Sales Order sudah dibatalkan sebelumnya.");

            // Release reservation if it was confirmed
            if (order.Status == SalesOrderStatus.Confirmed || order.Status == SalesOrderStatus.Picking)
            {
                foreach (var line in order.Lines)
                {
                    var releaseNeed = line.ReservedQuantity;
                    if (releaseNeed <= 0) continue;

                    var balances = await _context.InventoryBalances
                        .Where(b => b.ProductId == line.ProductId &&
                                    b.WarehouseId == order.WarehouseId &&
                                    b.Reserved > 0)
                        .ToListAsync(cancellationToken);

                    foreach (var b in balances)
                    {
                        if (releaseNeed <= 0) break;
                        var release = Math.Min(b.Reserved, releaseNeed);
                        b.Reserved -= release;
                        b.UpdatedAtUtc = _clock.UtcNow;
                        releaseNeed -= release;
                    }

                    line.ReservedQuantity = 0;
                }
            }

            var oldStatus = order.Status.ToString();
            order.Status = SalesOrderStatus.Cancelled;
            order.CancellationReason = reason;
            order.UpdatedAtUtc = _clock.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                nameof(SalesOrder),
                order.Id.ToString(),
                "Cancel",
                userId,
                oldStatus,
                $"Cancelled Sales Order: {reason}",
                reason,
                cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure($"Gagal membatalkan Sales Order: {ex.Message}");
        }
    }

    private static SalesOrderDto MapToDto(SalesOrder order)
    {
        return new SalesOrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            DeliveryDate = order.DeliveryDate,
            CustomerId = order.CustomerId,
            CustomerName = order.Customer?.Name ?? string.Empty,
            WarehouseId = order.WarehouseId,
            WarehouseName = order.Warehouse?.Name ?? string.Empty,
            Notes = order.Notes,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            CreatedByUserName = order.CreatedByUser?.FullName ?? order.CreatedByUser?.UserName,
            ConfirmedByUserName = order.ConfirmedByUser?.FullName ?? order.ConfirmedByUser?.UserName,
            DispatchedByUserName = order.DispatchedByUser?.FullName ?? order.DispatchedByUser?.UserName,
            Lines = order.Lines.Select(l => new SalesOrderLineDto
            {
                Id = l.Id,
                ProductId = l.ProductId,
                ProductSku = l.Product?.Sku ?? string.Empty,
                ProductName = l.Product?.Name ?? string.Empty,
                UnitName = l.Product?.Unit?.Name ?? string.Empty,
                OrderedQuantity = l.OrderedQuantity,
                ReservedQuantity = l.ReservedQuantity,
                DispatchedQuantity = l.DispatchedQuantity,
                UnitPrice = l.UnitPrice,
                Notes = l.Notes
            }).ToList()
        };
    }
}

