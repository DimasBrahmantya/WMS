using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Guards;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Audit;
using Wms.Application.Features.Operations.Returns;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Application.Features.Stock.Idempotency;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Operations;

public class StockReturnService : IStockReturnService
{
    private readonly IWmsDbContext _dbContext;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IAppAuditService _auditLogService;

    public StockReturnService(
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

    public async Task<Result<PaginatedList<StockReturnDto>>> GetReturnsAsync(
        StockReturnFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = _dbContext.StockReturns
            .AsNoTracking()
            .Include(r => r.Warehouse)
            .Include(r => r.Supplier)
            .Include(r => r.Customer)
            .Include(r => r.CreatedByUser)
            .Include(r => r.ApprovedByUser)
            .Include(r => r.PostedByUser)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(p => p.Unit)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Location)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            dbQuery = dbQuery.Where(r =>
                r.ReturnNumber.ToLower().Contains(term) ||
                (r.ReferenceDocumentNumber != null && r.ReferenceDocumentNumber.ToLower().Contains(term)) ||
                (r.Reason != null && r.Reason.ToLower().Contains(term)));
        }

        if (query.WarehouseId.HasValue)
            dbQuery = dbQuery.Where(r => r.WarehouseId == query.WarehouseId.Value);

        if (query.ReturnType.HasValue)
            dbQuery = dbQuery.Where(r => r.ReturnType == query.ReturnType.Value);

        if (query.Status.HasValue)
            dbQuery = dbQuery.Where(r => r.Status == query.Status.Value);

        if (query.StartDate.HasValue)
            dbQuery = dbQuery.Where(r => r.ReturnDate >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            dbQuery = dbQuery.Where(r => r.ReturnDate <= query.EndDate.Value);

        dbQuery = dbQuery.OrderByDescending(r => r.ReturnDate).ThenByDescending(r => r.CreatedAtUtc);

        var totalCount = await dbQuery.CountAsync(cancellationToken);
        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => MapToDto(r))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<StockReturnDto>>.Success(
            new PaginatedList<StockReturnDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<StockReturnDto>> GetReturnByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var r = await _dbContext.StockReturns
            .AsNoTracking()
            .Include(r => r.Warehouse)
            .Include(r => r.Supplier)
            .Include(r => r.Customer)
            .Include(r => r.CreatedByUser)
            .Include(r => r.ApprovedByUser)
            .Include(r => r.PostedByUser)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(p => p.Unit)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Location)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (r == null) return Result<StockReturnDto>.Failure("Dokumen retur tidak ditemukan.");

        return Result<StockReturnDto>.Success(MapToDto(r));
    }

    public async Task<Result<Guid>> CreateDraftAsync(
        CreateStockReturnCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var returnNumber = await _sequenceService.GetNextDocumentNumberAsync("RET", null, cancellationToken);

        var ret = new StockReturn
        {
            Id = Guid.NewGuid(),
            ReturnNumber = returnNumber,
            ReturnDate = DateTime.UtcNow,
            WarehouseId = command.WarehouseId,
            ReturnType = command.ReturnType,
            SupplierId = command.SupplierId,
            CustomerId = command.CustomerId,
            ReferenceDocumentNumber = command.ReferenceDocumentNumber,
            Reason = command.Reason,
            Notes = command.Notes,
            Status = StockReturnStatus.Draft,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        foreach (var line in command.Lines)
        {
            ret.Lines.Add(new StockReturnLine
            {
                Id = Guid.NewGuid(),
                StockReturnId = ret.Id,
                ProductId = line.ProductId,
                LocationId = line.LocationId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                TargetStockStatus = line.TargetStockStatus,
                Notes = line.Notes
            });
        }

        _dbContext.StockReturns.Add(ret);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockReturn",
            ret.Id.ToString(),
            "Create",
            userId,
            null,
            JsonSerializer.Serialize(new { ret.ReturnNumber, ret.ReturnType, LineCount = ret.Lines.Count }),
            $"Membuat draf retur {ret.ReturnNumber}",
            cancellationToken);

        return Result<Guid>.Success(ret.Id);
    }

    public async Task<Result> SubmitForApprovalAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var ret = await _dbContext.StockReturns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (ret == null) return Result.Failure("Dokumen retur tidak ditemukan.");

        if (ret.Status != StockReturnStatus.Draft)
            return Result.Failure("Hanya retur berstatus Draft yang dapat diajukan untuk approval.");

        ret.Status = StockReturnStatus.PendingApproval;
        ret.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockReturn",
            ret.Id.ToString(),
            "SubmitForApproval",
            userId,
            null,
            null,
            $"Mengajukan retur {ret.ReturnNumber} untuk approval",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ApproveAsync(
        Guid id,
        Guid approverUserId,
        CancellationToken cancellationToken = default)
    {
        var ret = await _dbContext.StockReturns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (ret == null) return Result.Failure("Dokumen retur tidak ditemukan.");

        if (ret.Status != StockReturnStatus.PendingApproval)
            return Result.Failure("Hanya retur berstatus PendingApproval yang dapat disetujui.");

        // Enforce Separation of Duties
        var sodResult = SeparationOfDutiesGuard.EnsureCanApprove(ret.CreatedByUserId, approverUserId, ret.ReturnNumber);
        if (!sodResult.Succeeded)
            return sodResult;

        ret.Status = StockReturnStatus.Approved;
        ret.ApprovedByUserId = approverUserId;
        ret.ApprovedAtUtc = DateTime.UtcNow;
        ret.RejectionReason = null;
        ret.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockReturn",
            ret.Id.ToString(),
            "Approve",
            approverUserId,
            null,
            null,
            $"Menyetujui retur {ret.ReturnNumber}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RejectAsync(
        Guid id,
        Guid approverUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var ret = await _dbContext.StockReturns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (ret == null) return Result.Failure("Dokumen retur tidak ditemukan.");

        if (ret.Status != StockReturnStatus.PendingApproval)
            return Result.Failure("Hanya retur berstatus PendingApproval yang dapat ditolak.");

        var sodResult = SeparationOfDutiesGuard.EnsureCanApprove(ret.CreatedByUserId, approverUserId, ret.ReturnNumber);
        if (!sodResult.Succeeded)
            return sodResult;

        ret.Status = StockReturnStatus.Rejected;
        ret.RejectionReason = reason;
        ret.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockReturn",
            ret.Id.ToString(),
            "Reject",
            approverUserId,
            null,
            reason,
            $"Menolak retur {ret.ReturnNumber}: {reason}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> PostAsync(
        Guid id,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _idempotencyService.ExecuteAsync(
            userId,
            nameof(PostAsync),
            idempotencyKey,
            async () =>
            {
                using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
                try
                {
                    var ret = await _dbContext.StockReturns
                        .Include(r => r.Lines)
                        .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

                    if (ret == null) return Result.Failure("Dokumen retur tidak ditemukan.");
                    if (ret.Status != StockReturnStatus.Approved)
                        return Result.Failure("Hanya retur yang telah disetujui (Approved) yang dapat diposting.");

                    var now = DateTime.UtcNow;

                    foreach (var line in ret.Lines)
                    {
                        if (ret.ReturnType == StockReturnType.ReturnToSupplier)
                        {
                            // 1. Kurangi stok dari gudang (StockStatus.Available)
                            var balance = await _dbContext.InventoryBalances
                                .FirstOrDefaultAsync(b => b.ProductId == line.ProductId &&
                                                          b.WarehouseId == ret.WarehouseId &&
                                                          b.WarehouseLocationId == (line.LocationId ?? Guid.Empty) &&
                                                          b.Status == StockStatus.Available, cancellationToken);

                            var balanceBefore = balance?.OnHand ?? 0m;
                            if (balanceBefore < line.Quantity)
                            {
                                return Result.Failure($"Stok tidak mencukupi untuk retur ke pemasok pada produk {line.ProductId}. Tersedia: {balanceBefore}, Diretur: {line.Quantity}");
                            }

                            balance!.OnHand -= line.Quantity;
                            balance.UpdatedAtUtc = now;
                            var balanceAfter = balance.OnHand;

                            // Valuasi rata-rata gudang berkurang
                            var valuation = await _dbContext.InventoryValuations
                                .FirstOrDefaultAsync(v => v.ProductId == line.ProductId && v.WarehouseId == ret.WarehouseId, cancellationToken);

                            var unitCost = line.UnitPrice > 0 ? line.UnitPrice : (valuation?.AvgCost ?? 0m);
                            var totalVal = Math.Round(line.Quantity * unitCost, 4, MidpointRounding.AwayFromZero);

                            if (valuation != null)
                            {
                                valuation.OnHandQty -= line.Quantity;
                                valuation.OnHandValue = Math.Max(0, valuation.OnHandValue - totalVal);
                                if (valuation.OnHandQty <= 0)
                                {
                                    valuation.OnHandQty = 0;
                                    valuation.OnHandValue = 0;
                                }
                                valuation.UpdatedAtUtc = now;
                            }

                            // Movement PurchaseReturn
                            var movementNumber = await _sequenceService.GetNextDocumentNumberAsync("MOV", now, cancellationToken);
                            var movement = new StockMovement
                            {
                                Id = Guid.NewGuid(),
                                MovementNumber = movementNumber,
                                MovementType = MovementType.PurchaseReturn,
                                ProductId = line.ProductId,
                                WarehouseId = ret.WarehouseId,
                                WarehouseLocationId = line.LocationId ?? Guid.Empty,
                                StockStatus = StockStatus.Available,
                                Quantity = line.Quantity,
                                Direction = MovementDirection.Out,
                                BalanceBefore = balanceBefore,
                                BalanceAfter = balanceAfter,
                                UnitCost = unitCost,
                                TotalValue = totalVal,
                                SourceDocumentType = SourceDocumentType.StockReturn,
                                SourceDocumentId = ret.Id,
                                SourceDocumentNumber = ret.ReturnNumber,
                                Reason = $"Retur ke pemasok {ret.SupplierId}. Alasan: {ret.Reason}",
                                UserId = userId,
                                TimestampUtc = now,
                                Status = MovementStatus.Posted
                            };
                            _dbContext.StockMovements.Add(movement);
                        }
                        else
                        {
                            // ReturnFromCustomer -> Tambah stok ke gudang (Available atau Damaged/Quarantine)
                            var targetStatus = line.TargetStockStatus;
                            var balance = await _dbContext.InventoryBalances
                                .FirstOrDefaultAsync(b => b.ProductId == line.ProductId &&
                                                          b.WarehouseId == ret.WarehouseId &&
                                                          b.WarehouseLocationId == (line.LocationId ?? Guid.Empty) &&
                                                          b.Status == targetStatus, cancellationToken);

                            var balanceBefore = balance?.OnHand ?? 0m;
                            var balanceAfter = balanceBefore + line.Quantity;

                            if (balance == null)
                            {
                                balance = new InventoryBalance
                                {
                                    Id = Guid.NewGuid(),
                                    ProductId = line.ProductId,
                                    WarehouseId = ret.WarehouseId,
                                    WarehouseLocationId = line.LocationId ?? Guid.Empty,
                                    Status = targetStatus,
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

                            var unitCost = line.UnitPrice;
                            var totalVal = Math.Round(line.Quantity * unitCost, 4, MidpointRounding.AwayFromZero);

                            // Jika masuk ke status Available, valuasi rata-rata bergerak dihitung ulang
                            if (targetStatus == StockStatus.Available)
                            {
                                var valuation = await _dbContext.InventoryValuations
                                    .FirstOrDefaultAsync(v => v.ProductId == line.ProductId && v.WarehouseId == ret.WarehouseId, cancellationToken);

                                if (valuation == null)
                                {
                                    valuation = new InventoryValuation
                                    {
                                        Id = Guid.NewGuid(),
                                        ProductId = line.ProductId,
                                        WarehouseId = ret.WarehouseId,
                                        OnHandQty = line.Quantity,
                                        OnHandValue = totalVal,
                                        AvgCost = unitCost,
                                        LastCost = unitCost,
                                        UpdatedAtUtc = now
                                    };
                                    _dbContext.InventoryValuations.Add(valuation);
                                }
                                else
                                {
                                    var newQty = valuation.OnHandQty + line.Quantity;
                                    var newVal = valuation.OnHandValue + totalVal;
                                    valuation.OnHandQty = newQty;
                                    valuation.OnHandValue = newVal;
                                    valuation.AvgCost = newQty > 0 ? Math.Round(newVal / newQty, 4, MidpointRounding.AwayFromZero) : unitCost;
                                    valuation.UpdatedAtUtc = now;
                                }
                            }

                            // Movement CustomerReturn
                            var movementNumber = await _sequenceService.GetNextDocumentNumberAsync("MOV", now, cancellationToken);
                            var movement = new StockMovement
                            {
                                Id = Guid.NewGuid(),
                                MovementNumber = movementNumber,
                                MovementType = MovementType.CustomerReturn,
                                ProductId = line.ProductId,
                                WarehouseId = ret.WarehouseId,
                                WarehouseLocationId = line.LocationId ?? Guid.Empty,
                                StockStatus = targetStatus,
                                Quantity = line.Quantity,
                                Direction = MovementDirection.In,
                                BalanceBefore = balanceBefore,
                                BalanceAfter = balanceAfter,
                                UnitCost = unitCost,
                                TotalValue = totalVal,
                                SourceDocumentType = SourceDocumentType.StockReturn,
                                SourceDocumentId = ret.Id,
                                SourceDocumentNumber = ret.ReturnNumber,
                                Reason = $"Retur dari pelanggan {ret.CustomerId} ke status {targetStatus}. Alasan: {ret.Reason}",
                                UserId = userId,
                                TimestampUtc = now,
                                Status = MovementStatus.Posted
                            };
                            _dbContext.StockMovements.Add(movement);
                        }
                    }

                    ret.Status = StockReturnStatus.Posted;
                    ret.PostedByUserId = userId;
                    ret.PostedAtUtc = now;
                    ret.UpdatedAtUtc = now;

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    await _auditLogService.LogAsync(
                        "StockReturn",
                        ret.Id.ToString(),
                        "Post",
                        userId,
                        null,
                        null,
                        $"Memposting retur {ret.ReturnNumber} ke ledger",
                        cancellationToken);

                    return Result.Success();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure($"Gagal memposting retur barang: {ex.Message}");
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
        var ret = await _dbContext.StockReturns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (ret == null) return Result.Failure("Dokumen retur tidak ditemukan.");

        if (ret.Status == StockReturnStatus.Posted)
            return Result.Failure("Retur yang telah diposting tidak dapat dibatalkan.");

        ret.Status = StockReturnStatus.Cancelled;
        ret.RejectionReason = reason;
        ret.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockReturn",
            ret.Id.ToString(),
            "Cancel",
            userId,
            null,
            reason,
            $"Membatalkan retur {ret.ReturnNumber}: {reason}",
            cancellationToken);

        return Result.Success();
    }

    private static StockReturnDto MapToDto(StockReturn r)
    {
        return new StockReturnDto
        {
            Id = r.Id,
            ReturnNumber = r.ReturnNumber,
            ReturnDate = r.ReturnDate,
            WarehouseId = r.WarehouseId,
            WarehouseName = r.Warehouse?.Name ?? string.Empty,
            ReturnType = r.ReturnType,
            SupplierId = r.SupplierId,
            SupplierName = r.Supplier?.Name,
            CustomerId = r.CustomerId,
            CustomerName = r.Customer?.Name,
            ReferenceDocumentNumber = r.ReferenceDocumentNumber,
            Reason = r.Reason,
            Notes = r.Notes,
            Status = r.Status,
            CreatedByUserId = r.CreatedByUserId,
            CreatedByUserName = r.CreatedByUser?.FullName ?? r.CreatedByUser?.UserName ?? string.Empty,
            ApprovedByUserId = r.ApprovedByUserId,
            ApprovedByUserName = r.ApprovedByUser?.FullName ?? r.ApprovedByUser?.UserName,
            ApprovedAtUtc = r.ApprovedAtUtc,
            RejectionReason = r.RejectionReason,
            PostedByUserId = r.PostedByUserId,
            PostedByUserName = r.PostedByUser?.FullName ?? r.PostedByUser?.UserName,
            PostedAtUtc = r.PostedAtUtc,
            CreatedAtUtc = r.CreatedAtUtc,
            ConcurrencyToken = r.ConcurrencyToken,
            Lines = r.Lines.Select(l => new StockReturnLineDto
            {
                Id = l.Id,
                StockReturnId = l.StockReturnId,
                ProductId = l.ProductId,
                ProductSku = l.Product?.Sku ?? string.Empty,
                ProductName = l.Product?.Name ?? string.Empty,
                UnitName = l.Product?.Unit?.Name ?? string.Empty,
                LocationId = l.LocationId,
                LocationName = l.Location?.Name,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TargetStockStatus = l.TargetStockStatus,
                Notes = l.Notes
            }).ToList()
        };
    }
}

