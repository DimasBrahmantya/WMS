using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Guards;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Audit;
using Wms.Application.Features.Operations.Transfers;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Application.Features.Stock.Idempotency;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Operations;

public class StockTransferService : IStockTransferService
{
    private readonly IWmsDbContext _dbContext;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IAppAuditService _auditLogService;

    public StockTransferService(
        IWmsDbContext dbContext,
        IDocumentSequenceService sequenceService,
        IIdempotencyService idempotencyService,
        IAppAuditService auditLogService)
    {
        _dbContext = dbContext;
        _sequenceService = sequenceService;
        _idempotencyService = idempotencyService;
        _auditLogService = auditLogService;
    }

    public async Task<Result<PaginatedList<StockTransferDto>>> GetStockTransfersAsync(
        StockTransferFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = _dbContext.StockTransfers
            .AsNoTracking()
            .Include(t => t.OriginWarehouse)
            .Include(t => t.DestinationWarehouse)
            .Include(t => t.CreatedByUser)
            .Include(t => t.ApprovedByUser)
            .Include(t => t.DispatchedByUser)
            .Include(t => t.ReceivedByUser)
            .Include(t => t.Lines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(p => p.Unit)
            .Include(t => t.Lines)
                .ThenInclude(l => l.OriginLocation)
            .Include(t => t.Lines)
                .ThenInclude(l => l.DestinationLocation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            dbQuery = dbQuery.Where(t =>
                t.TransferNumber.ToLower().Contains(term) ||
                t.OriginWarehouse.Name.ToLower().Contains(term) ||
                t.DestinationWarehouse.Name.ToLower().Contains(term) ||
                (t.Reason != null && t.Reason.ToLower().Contains(term)));
        }

        if (query.OriginWarehouseId.HasValue)
            dbQuery = dbQuery.Where(t => t.OriginWarehouseId == query.OriginWarehouseId.Value);

        if (query.DestinationWarehouseId.HasValue)
            dbQuery = dbQuery.Where(t => t.DestinationWarehouseId == query.DestinationWarehouseId.Value);

        if (query.Status.HasValue)
            dbQuery = dbQuery.Where(t => t.Status == query.Status.Value);

        if (query.StartDate.HasValue)
            dbQuery = dbQuery.Where(t => t.TransferDate >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            dbQuery = dbQuery.Where(t => t.TransferDate <= query.EndDate.Value);

        dbQuery = dbQuery.OrderByDescending(t => t.TransferDate).ThenByDescending(t => t.CreatedAtUtc);

        var totalCount = await dbQuery.CountAsync(cancellationToken);
        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => MapToDto(t))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<StockTransferDto>>.Success(
            new PaginatedList<StockTransferDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<StockTransferDto>> GetStockTransferByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var t = await _dbContext.StockTransfers
            .AsNoTracking()
            .Include(t => t.OriginWarehouse)
            .Include(t => t.DestinationWarehouse)
            .Include(t => t.CreatedByUser)
            .Include(t => t.ApprovedByUser)
            .Include(t => t.DispatchedByUser)
            .Include(t => t.ReceivedByUser)
            .Include(t => t.Lines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(p => p.Unit)
            .Include(t => t.Lines)
                .ThenInclude(l => l.OriginLocation)
            .Include(t => t.Lines)
                .ThenInclude(l => l.DestinationLocation)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (t == null)
            return Result<StockTransferDto>.Failure("Transfer antar-gudang tidak ditemukan.");

        return Result<StockTransferDto>.Success(MapToDto(t));
    }

    public async Task<Result<Guid>> CreateDraftAsync(
        CreateStockTransferCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (command.OriginWarehouseId == command.DestinationWarehouseId)
            return Result<Guid>.Failure("Gudang asal dan tujuan tidak boleh sama.");

        var transferNumber = await _sequenceService.GetNextDocumentNumberAsync("TRF", null, cancellationToken);

        var transfer = new StockTransfer
        {
            Id = Guid.NewGuid(),
            TransferNumber = transferNumber,
            TransferDate = DateTime.UtcNow,
            OriginWarehouseId = command.OriginWarehouseId,
            DestinationWarehouseId = command.DestinationWarehouseId,
            Reason = command.Reason,
            Notes = command.Notes,
            Status = StockTransferStatus.Draft,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        foreach (var line in command.Lines)
        {
            transfer.Lines.Add(new StockTransferLine
            {
                Id = Guid.NewGuid(),
                StockTransferId = transfer.Id,
                ProductId = line.ProductId,
                OriginLocationId = line.OriginLocationId,
                DestinationLocationId = line.DestinationLocationId,
                QuantityRequested = line.QuantityRequested,
                QuantityDispatched = 0,
                QuantityReceived = 0,
                Notes = line.Notes
            });
        }

        _dbContext.StockTransfers.Add(transfer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockTransfer",
            transfer.Id.ToString(),
            "Create",
            userId,
            null,
            JsonSerializer.Serialize(new { transfer.TransferNumber, LineCount = transfer.Lines.Count }),
            $"Membuat draf transfer antar-gudang {transfer.TransferNumber}",
            cancellationToken);

        return Result<Guid>.Success(transfer.Id);
    }

    public async Task<Result> SubmitForApprovalAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _dbContext.StockTransfers.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (transfer == null) return Result.Failure("Transfer antar-gudang tidak ditemukan.");

        if (transfer.Status != StockTransferStatus.Draft)
            return Result.Failure("Hanya transfer berstatus Draft yang dapat diajukan untuk approval.");

        transfer.Status = StockTransferStatus.PendingApproval;
        transfer.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockTransfer",
            transfer.Id.ToString(),
            "SubmitForApproval",
            userId,
            null,
            null,
            $"Mengajukan transfer {transfer.TransferNumber} untuk approval",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ApproveAsync(
        Guid id,
        Guid approverUserId,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _dbContext.StockTransfers.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (transfer == null) return Result.Failure("Transfer antar-gudang tidak ditemukan.");

        if (transfer.Status != StockTransferStatus.PendingApproval)
            return Result.Failure("Hanya transfer dengan status PendingApproval yang dapat disetujui.");

        // Enforce Separation of Duties
        var sodResult = SeparationOfDutiesGuard.EnsureCanApprove(transfer.CreatedByUserId, approverUserId, transfer.TransferNumber);
        if (!sodResult.Succeeded)
            return sodResult;

        transfer.Status = StockTransferStatus.Approved;
        transfer.ApprovedByUserId = approverUserId;
        transfer.ApprovedAtUtc = DateTime.UtcNow;
        transfer.RejectionReason = null;
        transfer.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockTransfer",
            transfer.Id.ToString(),
            "Approve",
            approverUserId,
            null,
            null,
            $"Menyetujui transfer {transfer.TransferNumber}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RejectAsync(
        Guid id,
        Guid approverUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _dbContext.StockTransfers.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (transfer == null) return Result.Failure("Transfer antar-gudang tidak ditemukan.");

        if (transfer.Status != StockTransferStatus.PendingApproval)
            return Result.Failure("Hanya transfer dengan status PendingApproval yang dapat ditolak.");

        var sodResult = SeparationOfDutiesGuard.EnsureCanApprove(transfer.CreatedByUserId, approverUserId, transfer.TransferNumber);
        if (!sodResult.Succeeded)
            return sodResult;

        transfer.Status = StockTransferStatus.Rejected;
        transfer.RejectionReason = reason;
        transfer.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockTransfer",
            transfer.Id.ToString(),
            "Reject",
            approverUserId,
            null,
            reason,
            $"Menolak transfer {transfer.TransferNumber}: {reason}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> DispatchAsync(
        Guid id,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _idempotencyService.ExecuteAsync(
            userId,
            nameof(DispatchAsync),
            idempotencyKey,
            async () =>
            {
                using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
                try
                {
                    var transfer = await _dbContext.StockTransfers
                        .Include(t => t.Lines)
                        .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

                    if (transfer == null) return Result.Failure("Transfer tidak ditemukan.");
                    if (transfer.Status != StockTransferStatus.Approved)
                        return Result.Failure("Hanya transfer yang telah disetujui (Approved) yang dapat dikirim.");

                    var now = DateTime.UtcNow;

                    foreach (var line in transfer.Lines)
                    {
                        // 1. Validasi saldo di gudang asal
                        var balanceQuery = _dbContext.InventoryBalances
                            .Where(b => b.ProductId == line.ProductId &&
                                        b.WarehouseId == transfer.OriginWarehouseId &&
                                        b.Status == StockStatus.Available);

                        if (line.OriginLocationId.HasValue)
                            balanceQuery = balanceQuery.Where(b => b.WarehouseLocationId == line.OriginLocationId.Value);

                        var balance = await balanceQuery.FirstOrDefaultAsync(cancellationToken);
                        var availableQty = balance?.Available ?? 0;

                        if (availableQty < line.QuantityRequested)
                        {
                            return Result.Failure($"Stok tidak mencukupi di gudang asal untuk produk ID {line.ProductId}. Tersedia: {availableQty}, Diminta: {line.QuantityRequested}");
                        }

                        // 2. Potong stok gudang asal
                        var balanceBefore = balance!.OnHand;
                        balance.OnHand -= line.QuantityRequested;
                        balance.UpdatedAtUtc = now;
                        var balanceAfter = balance.OnHand;

                        // 3. Valuasi pengeluaran transfer dinilai pada avg_cost gudang asal saat ini
                        var valuation = await _dbContext.InventoryValuations
                            .FirstOrDefaultAsync(v => v.ProductId == line.ProductId && v.WarehouseId == transfer.OriginWarehouseId, cancellationToken);

                        var unitCost = valuation?.AvgCost ?? 0m;
                        var totalVal = Math.Round(line.QuantityRequested * unitCost, 4, MidpointRounding.AwayFromZero);

                        if (valuation != null)
                        {
                            valuation.OnHandQty -= line.QuantityRequested;
                            valuation.OnHandValue = Math.Max(0, valuation.OnHandValue - totalVal);
                            if (valuation.OnHandQty <= 0)
                            {
                                valuation.OnHandQty = 0;
                                valuation.OnHandValue = 0;
                            }
                            valuation.UpdatedAtUtc = now;
                        }

                        // 4. Catat Stock Movement TransferOut
                        var movementNumber = await _sequenceService.GetNextDocumentNumberAsync("MOV", now, cancellationToken);
                        var movement = new StockMovement
                        {
                            Id = Guid.NewGuid(),
                            MovementNumber = movementNumber,
                            MovementType = MovementType.TransferOut,
                            ProductId = line.ProductId,
                            WarehouseId = transfer.OriginWarehouseId,
                            WarehouseLocationId = balance.WarehouseLocationId,
                            BatchNumber = balance.BatchNumber,
                            StockStatus = StockStatus.Available,
                            Quantity = line.QuantityRequested,
                            Direction = MovementDirection.Out,
                            BalanceBefore = balanceBefore,
                            BalanceAfter = balanceAfter,
                            UnitCost = unitCost,
                            TotalValue = totalVal,
                            SourceDocumentType = SourceDocumentType.StockTransfer,
                            SourceDocumentId = transfer.Id,
                            SourceDocumentNumber = transfer.TransferNumber,
                            Reason = $"Transfer keluar ke gudang tujuan {transfer.DestinationWarehouseId}",
                            UserId = userId,
                            TimestampUtc = now,
                            Status = MovementStatus.Posted
                        };
                        _dbContext.StockMovements.Add(movement);

                        line.QuantityDispatched = line.QuantityRequested;
                    }

                    transfer.Status = StockTransferStatus.InTransit;
                    transfer.DispatchedByUserId = userId;
                    transfer.DispatchedAtUtc = now;
                    transfer.UpdatedAtUtc = now;

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    await _auditLogService.LogAsync(
                        "StockTransfer",
                        transfer.Id.ToString(),
                        "Dispatch",
                        userId,
                        null,
                        null,
                        $"Mengirim transfer {transfer.TransferNumber} ke status InTransit",
                        cancellationToken);

                    return Result.Success();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure($"Gagal mengirim transfer antar-gudang: {ex.Message}");
                }
            },
            cancellationToken);
    }

    public async Task<Result> ReceiveAsync(
        Guid id,
        ReceiveStockTransferCommand command,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _idempotencyService.ExecuteAsync(
            userId,
            nameof(ReceiveAsync),
            idempotencyKey,
            async () =>
            {
                using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
                try
                {
                    var transfer = await _dbContext.StockTransfers
                        .Include(t => t.Lines)
                        .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

                    if (transfer == null) return Result.Failure("Transfer tidak ditemukan.");
                    if (transfer.Status != StockTransferStatus.InTransit && transfer.Status != StockTransferStatus.PartiallyReceived)
                        return Result.Failure("Hanya transfer berstatus InTransit atau PartiallyReceived yang dapat diterima.");

                    var now = DateTime.UtcNow;

                    foreach (var receiveLine in command.Lines)
                    {
                        var line = transfer.Lines.FirstOrDefault(l => l.Id == receiveLine.TransferLineId);
                        if (line == null)
                            return Result.Failure($"Baris transfer {receiveLine.TransferLineId} tidak ditemukan.");

                        if (line.QuantityReceived + receiveLine.QuantityReceived > line.QuantityDispatched)
                            return Result.Failure($"Kuantitas penerimaan melebihi kuantitas yang dikirim untuk baris barang {line.ProductId}.");

                        // Ambil biaya asal dari StockMovement TransferOut terkait
                        var outMovement = await _dbContext.StockMovements
                            .Where(m => m.SourceDocumentId == transfer.Id &&
                                        m.ProductId == line.ProductId &&
                                        m.MovementType == MovementType.TransferOut)
                            .FirstOrDefaultAsync(cancellationToken);

                        var unitCost = outMovement?.UnitCost ?? 0m;
                        var totalVal = Math.Round(receiveLine.QuantityReceived * unitCost, 4, MidpointRounding.AwayFromZero);

                        // 1. Tambah saldo di gudang tujuan
                        var balance = await _dbContext.InventoryBalances
                            .FirstOrDefaultAsync(b => b.ProductId == line.ProductId &&
                                                      b.WarehouseId == transfer.DestinationWarehouseId &&
                                                      b.WarehouseLocationId == receiveLine.DestinationLocationId &&
                                                      b.Status == StockStatus.Available, cancellationToken);

                        var balanceBefore = balance?.OnHand ?? 0m;
                        var balanceAfter = balanceBefore + receiveLine.QuantityReceived;

                        if (balance == null)
                        {
                            balance = new InventoryBalance
                            {
                                Id = Guid.NewGuid(),
                                ProductId = line.ProductId,
                                WarehouseId = transfer.DestinationWarehouseId,
                                WarehouseLocationId = receiveLine.DestinationLocationId,
                                Status = StockStatus.Available,
                                OnHand = balanceAfter,
                                Reserved = 0m,
                                CreatedAtUtc = now,
                                UpdatedAtUtc = now
                            };
                            _dbContext.InventoryBalances.Add(balance);
                        }
                        else
                        {
                            balance.OnHand = balanceAfter;
                            balance.UpdatedAtUtc = now;
                        }

                        // 2. Valuasi Rata-rata Bergerak Gudang Tujuan dihitung ulang
                        var valuation = await _dbContext.InventoryValuations
                            .FirstOrDefaultAsync(v => v.ProductId == line.ProductId && v.WarehouseId == transfer.DestinationWarehouseId, cancellationToken);

                        if (valuation == null)
                        {
                            valuation = new InventoryValuation
                            {
                                Id = Guid.NewGuid(),
                                ProductId = line.ProductId,
                                WarehouseId = transfer.DestinationWarehouseId,
                                OnHandQty = receiveLine.QuantityReceived,
                                OnHandValue = totalVal,
                                AvgCost = unitCost,
                                LastCost = unitCost,
                                UpdatedAtUtc = now
                            };
                            _dbContext.InventoryValuations.Add(valuation);
                        }
                        else
                        {
                            var oldQty = valuation.OnHandQty;
                            var oldVal = valuation.OnHandValue;
                            var newQty = oldQty + receiveLine.QuantityReceived;
                            var newVal = oldVal + totalVal;
                            var newAvg = newQty > 0 ? Math.Round(newVal / newQty, 4, MidpointRounding.AwayFromZero) : unitCost;

                            valuation.OnHandQty = newQty;
                            valuation.OnHandValue = newVal;
                            valuation.AvgCost = newAvg;
                            valuation.LastCost = unitCost;
                            valuation.UpdatedAtUtc = now;
                        }

                        // 3. Catat Movement TransferIn di gudang tujuan
                        var movementNumber = await _sequenceService.GetNextDocumentNumberAsync("MOV", now, cancellationToken);
                        var movement = new StockMovement
                        {
                            Id = Guid.NewGuid(),
                            MovementNumber = movementNumber,
                            MovementType = MovementType.TransferIn,
                            ProductId = line.ProductId,
                            WarehouseId = transfer.DestinationWarehouseId,
                            WarehouseLocationId = receiveLine.DestinationLocationId,
                            StockStatus = StockStatus.Available,
                            Quantity = receiveLine.QuantityReceived,
                            Direction = MovementDirection.In,
                            BalanceBefore = balanceBefore,
                            BalanceAfter = balanceAfter,
                            UnitCost = unitCost,
                            TotalValue = totalVal,
                            SourceDocumentType = SourceDocumentType.StockTransfer,
                            SourceDocumentId = transfer.Id,
                            SourceDocumentNumber = transfer.TransferNumber,
                            Reason = $"Penerimaan transfer dari gudang asal {transfer.OriginWarehouseId}",
                            UserId = userId,
                            TimestampUtc = now,
                            Status = MovementStatus.Posted
                        };
                        _dbContext.StockMovements.Add(movement);

                        line.QuantityReceived += receiveLine.QuantityReceived;
                        line.DestinationLocationId = receiveLine.DestinationLocationId;
                    }

                    bool allFulfilled = transfer.Lines.All(l => l.QuantityReceived >= l.QuantityDispatched);
                    transfer.Status = allFulfilled ? StockTransferStatus.Received : StockTransferStatus.PartiallyReceived;
                    transfer.ReceivedByUserId = userId;
                    transfer.ReceivedAtUtc = now;
                    transfer.UpdatedAtUtc = now;

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    await _auditLogService.LogAsync(
                        "StockTransfer",
                        transfer.Id.ToString(),
                        "Receive",
                        userId,
                        null,
                        null,
                        $"Menerima transfer {transfer.TransferNumber} di gudang tujuan",
                        cancellationToken);

                    return Result.Success();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure($"Gagal menerima transfer: {ex.Message}");
                }
            },
            cancellationToken);
    }

    public async Task<Result> CancelAsync(
        Guid id,
        Guid userId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _dbContext.StockTransfers.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (transfer == null) return Result.Failure("Transfer tidak ditemukan.");

        if (transfer.Status == StockTransferStatus.InTransit ||
            transfer.Status == StockTransferStatus.PartiallyReceived ||
            transfer.Status == StockTransferStatus.Received)
        {
            return Result.Failure("Transfer yang telah dikirim (InTransit/Received) tidak dapat dibatalkan langsung.");
        }

        transfer.Status = StockTransferStatus.Cancelled;
        transfer.RejectionReason = reason;
        transfer.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockTransfer",
            transfer.Id.ToString(),
            "Cancel",
            userId,
            null,
            reason,
            $"Membatalkan transfer {transfer.TransferNumber}: {reason}",
            cancellationToken);

        return Result.Success();
    }

    private static StockTransferDto MapToDto(StockTransfer t)
    {
        return new StockTransferDto
        {
            Id = t.Id,
            TransferNumber = t.TransferNumber,
            TransferDate = t.TransferDate,
            OriginWarehouseId = t.OriginWarehouseId,
            OriginWarehouseName = t.OriginWarehouse?.Name ?? string.Empty,
            DestinationWarehouseId = t.DestinationWarehouseId,
            DestinationWarehouseName = t.DestinationWarehouse?.Name ?? string.Empty,
            Reason = t.Reason,
            Notes = t.Notes,
            Status = t.Status,
            CreatedByUserId = t.CreatedByUserId,
            CreatedByUserName = t.CreatedByUser?.FullName ?? t.CreatedByUser?.UserName ?? string.Empty,
            ApprovedByUserId = t.ApprovedByUserId,
            ApprovedByUserName = t.ApprovedByUser?.FullName ?? t.ApprovedByUser?.UserName,
            ApprovedAtUtc = t.ApprovedAtUtc,
            RejectionReason = t.RejectionReason,
            DispatchedByUserId = t.DispatchedByUserId,
            DispatchedByUserName = t.DispatchedByUser?.FullName ?? t.DispatchedByUser?.UserName,
            DispatchedAtUtc = t.DispatchedAtUtc,
            ReceivedByUserId = t.ReceivedByUserId,
            ReceivedByUserName = t.ReceivedByUser?.FullName ?? t.ReceivedByUser?.UserName,
            ReceivedAtUtc = t.ReceivedAtUtc,
            CreatedAtUtc = t.CreatedAtUtc,
            ConcurrencyToken = t.ConcurrencyToken,
            Lines = t.Lines.Select(l => new StockTransferLineDto
            {
                Id = l.Id,
                StockTransferId = l.StockTransferId,
                ProductId = l.ProductId,
                ProductSku = l.Product?.Sku ?? string.Empty,
                ProductName = l.Product?.Name ?? string.Empty,
                UnitName = l.Product?.Unit?.Name ?? string.Empty,
                OriginLocationId = l.OriginLocationId,
                OriginLocationName = l.OriginLocation?.Name,
                DestinationLocationId = l.DestinationLocationId,
                DestinationLocationName = l.DestinationLocation?.Name,
                QuantityRequested = l.QuantityRequested,
                QuantityDispatched = l.QuantityDispatched,
                QuantityReceived = l.QuantityReceived,
                RemainingQuantity = l.RemainingQuantity,
                Notes = l.Notes
            }).ToList()
        };
    }
}
