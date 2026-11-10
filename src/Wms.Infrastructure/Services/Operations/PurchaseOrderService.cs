using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Guards;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Audit;
using Wms.Application.Features.Operations.PurchaseOrders;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Operations;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IWmsDbContext _dbContext;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IAppAuditService _auditLogService;

    public PurchaseOrderService(
        IWmsDbContext dbContext,
        IDocumentSequenceService sequenceService,
        IAppAuditService auditLogService)
    {
        _dbContext = dbContext;
        _sequenceService = sequenceService;
        _auditLogService = auditLogService;
    }

    public async Task<Result<PaginatedList<PurchaseOrderDto>>> GetPurchaseOrdersAsync(
        PurchaseOrderFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.CreatedByUser)
            .Include(p => p.ApprovedByUser)
            .Include(p => p.Lines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(pr => pr.Unit)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            dbQuery = dbQuery.Where(p =>
                p.PoNumber.ToLower().Contains(term) ||
                p.Supplier.Name.ToLower().Contains(term) ||
                (p.Notes != null && p.Notes.ToLower().Contains(term)));
        }

        if (query.SupplierId.HasValue)
            dbQuery = dbQuery.Where(p => p.SupplierId == query.SupplierId.Value);

        if (query.WarehouseId.HasValue)
            dbQuery = dbQuery.Where(p => p.WarehouseId == query.WarehouseId.Value);

        if (query.Status.HasValue)
            dbQuery = dbQuery.Where(p => p.Status == query.Status.Value);

        if (query.StartDate.HasValue)
            dbQuery = dbQuery.Where(p => p.OrderDate >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            dbQuery = dbQuery.Where(p => p.OrderDate <= query.EndDate.Value);

        dbQuery = dbQuery.OrderByDescending(p => p.OrderDate).ThenByDescending(p => p.CreatedAtUtc);

        var totalCount = await dbQuery.CountAsync(cancellationToken);
        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => MapToDto(p))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<PurchaseOrderDto>>.Success(
            new PaginatedList<PurchaseOrderDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<PurchaseOrderDto>> GetPurchaseOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.CreatedByUser)
            .Include(p => p.ApprovedByUser)
            .Include(p => p.Lines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(pr => pr.Unit)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (po == null)
            return Result<PurchaseOrderDto>.Failure("Purchase Order tidak ditemukan.");

        return Result<PurchaseOrderDto>.Success(MapToDto(po));
    }

    public async Task<Result<Guid>> CreatePurchaseOrderAsync(
        CreatePurchaseOrderCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var poNumber = await _sequenceService.GetNextDocumentNumberAsync("PO", null, cancellationToken);

        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = poNumber,
            OrderDate = DateTime.UtcNow,
            ExpectedDeliveryDate = command.ExpectedDeliveryDate,
            SupplierId = command.SupplierId,
            WarehouseId = command.WarehouseId,
            Notes = command.Notes,
            Status = PurchaseOrderStatus.Draft,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        decimal total = 0;
        foreach (var line in command.Lines)
        {
            var poLine = new PurchaseOrderLine
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = po.Id,
                ProductId = line.ProductId,
                OrderedQuantity = line.OrderedQuantity,
                ReceivedQuantity = 0,
                UnitPrice = line.UnitPrice,
                Notes = line.Notes
            };
            total += poLine.Subtotal;
            po.Lines.Add(poLine);
        }

        po.TotalAmount = total;

        _dbContext.PurchaseOrders.Add(po);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "PurchaseOrder",
            po.Id.ToString(),
            "Create",
            userId,
            null,
            JsonSerializer.Serialize(new { po.PoNumber, po.TotalAmount, LineCount = po.Lines.Count }),
            $"Membuat Purchase Order {po.PoNumber}",
            cancellationToken);

        return Result<Guid>.Success(po.Id);
    }

    public async Task<Result<Guid>> UpdatePurchaseOrderAsync(
        Guid id,
        UpdatePurchaseOrderCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (po == null)
            return Result<Guid>.Failure("Purchase Order tidak ditemukan.");

        if (po.Status != PurchaseOrderStatus.Draft)
            return Result<Guid>.Failure("Hanya Purchase Order berstatus Draft yang dapat diubah.");

        po.SupplierId = command.SupplierId;
        po.WarehouseId = command.WarehouseId;
        po.ExpectedDeliveryDate = command.ExpectedDeliveryDate;
        po.Notes = command.Notes;
        po.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.PurchaseOrderLines.RemoveRange(po.Lines);
        po.Lines.Clear();

        decimal total = 0;
        foreach (var line in command.Lines)
        {
            var poLine = new PurchaseOrderLine
            {
                Id = line.Id ?? Guid.NewGuid(),
                PurchaseOrderId = po.Id,
                ProductId = line.ProductId,
                OrderedQuantity = line.OrderedQuantity,
                ReceivedQuantity = 0,
                UnitPrice = line.UnitPrice,
                Notes = line.Notes
            };
            total += poLine.Subtotal;
            po.Lines.Add(poLine);
        }

        po.TotalAmount = total;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(po.Id);
    }

    public async Task<Result> SubmitForApprovalAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (po == null) return Result.Failure("Purchase Order tidak ditemukan.");

        if (po.Status != PurchaseOrderStatus.Draft)
            return Result.Failure("Hanya PO berstatus Draft yang dapat diajukan untuk approval.");

        po.Status = PurchaseOrderStatus.PendingApproval;
        po.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "PurchaseOrder",
            po.Id.ToString(),
            "SubmitForApproval",
            userId,
            null,
            null,
            $"Mengajukan PO {po.PoNumber} untuk approval",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ApprovePurchaseOrderAsync(
        Guid id,
        Guid approverUserId,
        CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (po == null) return Result.Failure("Purchase Order tidak ditemukan.");

        if (po.Status != PurchaseOrderStatus.PendingApproval)
            return Result.Failure("Hanya PO dengan status PendingApproval yang dapat disetujui.");

        // Enforce Separation of Duties
        var sodResult = SeparationOfDutiesGuard.EnsureCanApprove(po.CreatedByUserId, approverUserId, po.PoNumber);
        if (!sodResult.Succeeded)
            return sodResult;

        po.Status = PurchaseOrderStatus.Approved;
        po.ApprovedByUserId = approverUserId;
        po.ApprovedAtUtc = DateTime.UtcNow;
        po.RejectionReason = null;
        po.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "PurchaseOrder",
            po.Id.ToString(),
            "Approve",
            approverUserId,
            null,
            null,
            $"Menyetujui Purchase Order {po.PoNumber}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RejectPurchaseOrderAsync(
        Guid id,
        Guid approverUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (po == null) return Result.Failure("Purchase Order tidak ditemukan.");

        if (po.Status != PurchaseOrderStatus.PendingApproval)
            return Result.Failure("Hanya PO dengan status PendingApproval yang dapat ditolak.");

        // Enforce Separation of Duties
        var sodResult = SeparationOfDutiesGuard.EnsureCanApprove(po.CreatedByUserId, approverUserId, po.PoNumber);
        if (!sodResult.Succeeded)
            return sodResult;

        po.Status = PurchaseOrderStatus.Rejected;
        po.RejectionReason = reason;
        po.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "PurchaseOrder",
            po.Id.ToString(),
            "Reject",
            approverUserId,
            null,
            reason,
            $"Menolak Purchase Order {po.PoNumber} dengan alasan: {reason}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> MarkAsOrderedAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (po == null) return Result.Failure("Purchase Order tidak ditemukan.");

        if (po.Status != PurchaseOrderStatus.Approved)
            return Result.Failure("Hanya PO yang telah disetujui (Approved) yang dapat ditandai telah dipesan (Ordered).");

        po.Status = PurchaseOrderStatus.Ordered;
        po.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "PurchaseOrder",
            po.Id.ToString(),
            "Order",
            userId,
            null,
            null,
            $"Mengirim/memesan PO {po.PoNumber} ke pemasok",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> CancelPurchaseOrderAsync(
        Guid id,
        Guid userId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (po == null) return Result.Failure("Purchase Order tidak ditemukan.");

        if (po.Status == PurchaseOrderStatus.Received)
            return Result.Failure("PO yang telah diterima penuh tidak dapat dibatalkan.");

        if (po.Lines.Any(l => l.ReceivedQuantity > 0))
            return Result.Failure("PO yang telah memiliki penerimaan parsial tidak dapat dibatalkan secara langsung.");

        po.Status = PurchaseOrderStatus.Cancelled;
        po.RejectionReason = reason;
        po.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "PurchaseOrder",
            po.Id.ToString(),
            "Cancel",
            userId,
            null,
            reason,
            $"Membatalkan PO {po.PoNumber}: {reason}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RecordReceiptAsync(
        Guid poId,
        List<(Guid poLineId, decimal quantity)> receivedItems,
        CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == poId, cancellationToken);

        if (po == null)
            return Result.Failure("Purchase Order tidak ditemukan.");

        if (po.Status != PurchaseOrderStatus.Ordered && po.Status != PurchaseOrderStatus.PartiallyReceived)
            return Result.Failure($"PO berstatus '{po.Status}' tidak dapat menerima barang. Status harus 'Ordered' atau 'PartiallyReceived'.");

        foreach (var (poLineId, qty) in receivedItems)
        {
            var line = po.Lines.FirstOrDefault(l => l.Id == poLineId);
            if (line == null)
                return Result.Failure($"Baris PO dengan ID {poLineId} tidak ditemukan pada PO ini.");

            if (line.ReceivedQuantity + qty > line.OrderedQuantity)
                return Result.Failure($"Kuantitas penerimaan ({line.ReceivedQuantity + qty}) melebihi kuantitas pesanan ({line.OrderedQuantity}) untuk baris barang ini.");

            line.ReceivedQuantity += qty;
        }

        // Hitung ulang status PO
        bool allFulfilled = po.Lines.All(l => l.ReceivedQuantity >= l.OrderedQuantity);
        po.Status = allFulfilled ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
        po.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RollbackReceiptAsync(
        Guid poId,
        List<(Guid poLineId, decimal quantity)> rolledBackItems,
        CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == poId, cancellationToken);

        if (po == null)
            return Result.Failure("Purchase Order tidak ditemukan.");

        foreach (var (poLineId, qty) in rolledBackItems)
        {
            var line = po.Lines.FirstOrDefault(l => l.Id == poLineId);
            if (line != null)
            {
                line.ReceivedQuantity = Math.Max(0, line.ReceivedQuantity - qty);
            }
        }

        bool anyReceived = po.Lines.Any(l => l.ReceivedQuantity > 0);
        po.Status = anyReceived ? PurchaseOrderStatus.PartiallyReceived : PurchaseOrderStatus.Ordered;
        po.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static PurchaseOrderDto MapToDto(PurchaseOrder po)
    {
        return new PurchaseOrderDto
        {
            Id = po.Id,
            PoNumber = po.PoNumber,
            OrderDate = po.OrderDate,
            ExpectedDeliveryDate = po.ExpectedDeliveryDate,
            SupplierId = po.SupplierId,
            SupplierName = po.Supplier?.Name ?? string.Empty,
            WarehouseId = po.WarehouseId,
            WarehouseName = po.Warehouse?.Name ?? string.Empty,
            Notes = po.Notes,
            Status = po.Status,
            TotalAmount = po.TotalAmount,
            CreatedByUserId = po.CreatedByUserId,
            CreatedByUserName = po.CreatedByUser?.FullName ?? po.CreatedByUser?.UserName ?? string.Empty,
            ApprovedByUserId = po.ApprovedByUserId,
            ApprovedByUserName = po.ApprovedByUser?.FullName ?? po.ApprovedByUser?.UserName,
            ApprovedAtUtc = po.ApprovedAtUtc,
            RejectionReason = po.RejectionReason,
            CreatedAtUtc = po.CreatedAtUtc,
            ConcurrencyToken = po.ConcurrencyToken,
            Lines = po.Lines.Select(l => new PurchaseOrderLineDto
            {
                Id = l.Id,
                PurchaseOrderId = l.PurchaseOrderId,
                ProductId = l.ProductId,
                ProductSku = l.Product?.Sku ?? string.Empty,
                ProductName = l.Product?.Name ?? string.Empty,
                UnitName = l.Product?.Unit?.Name ?? string.Empty,
                OrderedQuantity = l.OrderedQuantity,
                ReceivedQuantity = l.ReceivedQuantity,
                RemainingQuantity = l.RemainingQuantity,
                UnitPrice = l.UnitPrice,
                Subtotal = l.Subtotal,
                Notes = l.Notes
            }).ToList()
        };
    }
}
