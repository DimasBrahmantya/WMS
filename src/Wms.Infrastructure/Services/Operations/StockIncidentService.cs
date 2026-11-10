using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Guards;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Audit;
using Wms.Application.Features.Operations.Incidents;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Application.Features.Stock.Idempotency;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Operations;

public class StockIncidentService : IStockIncidentService
{
    private readonly IWmsDbContext _dbContext;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IAppAuditService _auditLogService;

    public StockIncidentService(
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

    public async Task<Result<PaginatedList<StockIncidentDto>>> GetIncidentsAsync(
        StockIncidentFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = _dbContext.StockIncidents
            .AsNoTracking()
            .Include(i => i.Warehouse)
            .Include(i => i.Product)
                .ThenInclude(p => p.Unit)
            .Include(i => i.Location)
            .Include(i => i.ReportedByUser)
            .Include(i => i.InvestigatedByUser)
            .Include(i => i.ApprovedByUser)
            .Include(i => i.ResolvedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            dbQuery = dbQuery.Where(i =>
                i.IncidentNumber.ToLower().Contains(term) ||
                i.Product.Name.ToLower().Contains(term) ||
                i.Product.Sku.ToLower().Contains(term) ||
                (i.Reason != null && i.Reason.ToLower().Contains(term)));
        }

        if (query.WarehouseId.HasValue)
            dbQuery = dbQuery.Where(i => i.WarehouseId == query.WarehouseId.Value);

        if (query.Category.HasValue)
            dbQuery = dbQuery.Where(i => i.Category == query.Category.Value);

        if (query.Status.HasValue)
            dbQuery = dbQuery.Where(i => i.Status == query.Status.Value);

        if (query.StartDate.HasValue)
            dbQuery = dbQuery.Where(i => i.IncidentDate >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            dbQuery = dbQuery.Where(i => i.IncidentDate <= query.EndDate.Value);

        dbQuery = dbQuery.OrderByDescending(i => i.IncidentDate).ThenByDescending(i => i.ReportedAtUtc);

        var totalCount = await dbQuery.CountAsync(cancellationToken);
        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(i => MapToDto(i))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<StockIncidentDto>>.Success(
            new PaginatedList<StockIncidentDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<StockIncidentDto>> GetIncidentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var i = await _dbContext.StockIncidents
            .AsNoTracking()
            .Include(i => i.Warehouse)
            .Include(i => i.Product)
                .ThenInclude(p => p.Unit)
            .Include(i => i.Location)
            .Include(i => i.ReportedByUser)
            .Include(i => i.InvestigatedByUser)
            .Include(i => i.ApprovedByUser)
            .Include(i => i.ResolvedByUser)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (i == null) return Result<StockIncidentDto>.Failure("Laporan kejadian hilang/rusak tidak ditemukan.");

        return Result<StockIncidentDto>.Success(MapToDto(i));
    }

    public async Task<Result<Guid>> ReportIncidentAsync(
        CreateStockIncidentCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var incidentNumber = await _sequenceService.GetNextDocumentNumberAsync("INC", null, cancellationToken);

        // Ambil perkiraan nilai dari avg_cost saat dilaporkan
        var valuation = await _dbContext.InventoryValuations
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ProductId == command.ProductId && v.WarehouseId == command.WarehouseId, cancellationToken);

        var avgCost = valuation?.AvgCost ?? 0m;
        var estimatedVal = Math.Round(command.Quantity * avgCost, 4, MidpointRounding.AwayFromZero);

        var incident = new StockIncident
        {
            Id = Guid.NewGuid(),
            IncidentNumber = incidentNumber,
            IncidentDate = DateTime.UtcNow,
            ReportedAtUtc = DateTime.UtcNow,
            WarehouseId = command.WarehouseId,
            ProductId = command.ProductId,
            LocationId = command.LocationId,
            Category = command.Category,
            Quantity = command.Quantity,
            EstimatedValue = estimatedVal,
            Reason = command.Reason,
            InvestigationNotes = command.InvestigationNotes,
            Status = StockIncidentStatus.Reported,
            ReportedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.StockIncidents.Add(incident);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockIncident",
            incident.Id.ToString(),
            "Report",
            userId,
            null,
            JsonSerializer.Serialize(new { incident.IncidentNumber, incident.Category, incident.Quantity }),
            $"Melaporkan kejadian {incident.Category}: {incident.IncidentNumber}",
            cancellationToken);

        return Result<Guid>.Success(incident.Id);
    }

    public async Task<Result> UpdateInvestigationAsync(
        Guid id,
        string investigationNotes,
        Guid investigatorUserId,
        CancellationToken cancellationToken = default)
    {
        var incident = await _dbContext.StockIncidents.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident == null) return Result.Failure("Kejadian tidak ditemukan.");

        incident.InvestigationNotes = investigationNotes;
        incident.InvestigatedByUserId = investigatorUserId;
        incident.Status = StockIncidentStatus.UnderInvestigation;
        incident.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ApproveAsync(
        Guid id,
        Guid approverUserId,
        CancellationToken cancellationToken = default)
    {
        var incident = await _dbContext.StockIncidents.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident == null) return Result.Failure("Kejadian tidak ditemukan.");

        if (incident.Status != StockIncidentStatus.Reported && incident.Status != StockIncidentStatus.UnderInvestigation)
            return Result.Failure("Hanya kejadian yang dilaporkan atau sedang diinvestigasi yang dapat disetujui.");

        // Enforce Separation of Duties: Reporter cannot approve own incident
        var sodResult = SeparationOfDutiesGuard.EnsureCanApprove(incident.ReportedByUserId, approverUserId, incident.IncidentNumber);
        if (!sodResult.Succeeded)
            return sodResult;

        incident.Status = StockIncidentStatus.Approved;
        incident.ApprovedByUserId = approverUserId;
        incident.ApprovedAtUtc = DateTime.UtcNow;
        incident.RejectionReason = null;
        incident.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockIncident",
            incident.Id.ToString(),
            "Approve",
            approverUserId,
            null,
            null,
            $"Menyetujui laporan kejadian {incident.IncidentNumber}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RejectAsync(
        Guid id,
        Guid approverUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var incident = await _dbContext.StockIncidents.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident == null) return Result.Failure("Kejadian tidak ditemukan.");

        if (incident.Status != StockIncidentStatus.Reported && incident.Status != StockIncidentStatus.UnderInvestigation)
            return Result.Failure("Hanya kejadian yang dilaporkan atau sedang diinvestigasi yang dapat ditolak.");

        var sodResult = SeparationOfDutiesGuard.EnsureCanApprove(incident.ReportedByUserId, approverUserId, incident.IncidentNumber);
        if (!sodResult.Succeeded)
            return sodResult;

        incident.Status = StockIncidentStatus.Rejected;
        incident.RejectionReason = reason;
        incident.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockIncident",
            incident.Id.ToString(),
            "Reject",
            approverUserId,
            null,
            reason,
            $"Menolak laporan kejadian {incident.IncidentNumber}: {reason}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ResolveAsync(
        Guid id,
        string actionTaken,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _idempotencyService.ExecuteAsync(
            userId,
            nameof(ResolveAsync),
            idempotencyKey,
            async () =>
            {
                using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
                try
                {
                    var incident = await _dbContext.StockIncidents
                        .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

                    if (incident == null) return Result.Failure("Kejadian tidak ditemukan.");
                    if (incident.Status != StockIncidentStatus.Approved)
                        return Result.Failure("Hanya kejadian yang telah disetujui (Approved) yang dapat ditindaklanjuti/diposting.");

                    var now = DateTime.UtcNow;

                    // 1. Ambil saldo Available di gudang dan lokasi kejadian
                    var balance = await _dbContext.InventoryBalances
                        .FirstOrDefaultAsync(b => b.ProductId == incident.ProductId &&
                                                  b.WarehouseId == incident.WarehouseId &&
                                                  b.WarehouseLocationId == (incident.LocationId ?? Guid.Empty) &&
                                                  b.Status == StockStatus.Available, cancellationToken);

                    var balanceBefore = balance?.OnHand ?? 0m;
                    if (balanceBefore < incident.Quantity)
                    {
                        return Result.Failure($"Stok tersedia tidak mencukupi untuk memproses kejadian ini. Tersedia: {balanceBefore}, Kejadian: {incident.Quantity}");
                    }

                    // Ambil valuasi rata-rata
                    var valuation = await _dbContext.InventoryValuations
                        .FirstOrDefaultAsync(v => v.ProductId == incident.ProductId && v.WarehouseId == incident.WarehouseId, cancellationToken);

                    var unitCost = valuation?.AvgCost ?? 0m;
                    var totalVal = Math.Round(incident.Quantity * unitCost, 4, MidpointRounding.AwayFromZero);

                    if (incident.Category == StockIncidentCategory.Lost)
                    {
                        // BARANG HILANG: Potong stok on_hand dari sistem
                        balance!.OnHand -= incident.Quantity;
                        balance.UpdatedAtUtc = now;
                        var balanceAfter = balance.OnHand;

                        if (valuation != null)
                        {
                            valuation.OnHandQty -= incident.Quantity;
                            valuation.OnHandValue = Math.Max(0, valuation.OnHandValue - totalVal);
                            if (valuation.OnHandQty <= 0)
                            {
                                valuation.OnHandQty = 0;
                                valuation.OnHandValue = 0;
                            }
                            valuation.UpdatedAtUtc = now;
                        }

                        var movementNumber = await _sequenceService.GetNextDocumentNumberAsync("MOV", now, cancellationToken);
                        var movement = new StockMovement
                        {
                            Id = Guid.NewGuid(),
                            MovementNumber = movementNumber,
                            MovementType = MovementType.StockIncident,
                            ProductId = incident.ProductId,
                            WarehouseId = incident.WarehouseId,
                            WarehouseLocationId = incident.LocationId ?? Guid.Empty,
                            StockStatus = StockStatus.Available,
                            Quantity = incident.Quantity,
                            Direction = MovementDirection.Out,
                            BalanceBefore = balanceBefore,
                            BalanceAfter = balanceAfter,
                            UnitCost = unitCost,
                            TotalValue = totalVal,
                            SourceDocumentType = SourceDocumentType.StockIncident,
                            SourceDocumentId = incident.Id,
                            SourceDocumentNumber = incident.IncidentNumber,
                            Reason = $"Penghapusan barang hilang: {incident.IncidentNumber}. Tindakan: {actionTaken}",
                            UserId = userId,
                            TimestampUtc = now,
                            Status = MovementStatus.Posted
                        };
                        _dbContext.StockMovements.Add(movement);
                    }
                    else
                    {
                        // BARANG RUSAK / EXPIRED / KARANTINA: Pindahkan status stok dari Available ke Damaged / Quarantine
                        var targetStatus = (incident.Category == StockIncidentCategory.Quarantine)
                            ? StockStatus.Quarantine
                            : StockStatus.Damaged;

                        balance!.OnHand -= incident.Quantity;
                        balance.UpdatedAtUtc = now;

                        // Tambah atau buat balance status target
                        var targetBalance = await _dbContext.InventoryBalances
                            .FirstOrDefaultAsync(b => b.ProductId == incident.ProductId &&
                                                      b.WarehouseId == incident.WarehouseId &&
                                                      b.WarehouseLocationId == (incident.LocationId ?? Guid.Empty) &&
                                                      b.Status == targetStatus, cancellationToken);

                        if (targetBalance == null)
                        {
                            targetBalance = new InventoryBalance
                            {
                                Id = Guid.NewGuid(),
                                ProductId = incident.ProductId,
                                WarehouseId = incident.WarehouseId,
                                WarehouseLocationId = incident.LocationId ?? Guid.Empty,
                                Status = targetStatus,
                                OnHand = incident.Quantity,
                                Reserved = 0m,
                                CreatedAtUtc = now,
                                UpdatedAtUtc = now
                            };
                            _dbContext.InventoryBalances.Add(targetBalance);
                        }
                        else
                        {
                            targetBalance.OnHand += incident.Quantity;
                            targetBalance.UpdatedAtUtc = now;
                        }

                        // Movement reposisi
                        var movementNumber = await _sequenceService.GetNextDocumentNumberAsync("MOV", now, cancellationToken);
                        var movement = new StockMovement
                        {
                            Id = Guid.NewGuid(),
                            MovementNumber = movementNumber,
                            MovementType = MovementType.StockIncident,
                            ProductId = incident.ProductId,
                            WarehouseId = incident.WarehouseId,
                            WarehouseLocationId = incident.LocationId ?? Guid.Empty,
                            StockStatus = targetStatus,
                            Quantity = incident.Quantity,
                            Direction = MovementDirection.In,
                            BalanceBefore = balanceBefore,
                            BalanceAfter = balance.OnHand,
                            UnitCost = unitCost,
                            TotalValue = totalVal,
                            SourceDocumentType = SourceDocumentType.StockIncident,
                            SourceDocumentId = incident.Id,
                            SourceDocumentNumber = incident.IncidentNumber,
                            Reason = $"Pemindahan stok ke {targetStatus} akibat {incident.Category}. Tindakan: {actionTaken}",
                            UserId = userId,
                            TimestampUtc = now,
                            Status = MovementStatus.Posted
                        };
                        _dbContext.StockMovements.Add(movement);
                    }

                    incident.Status = StockIncidentStatus.Resolved;
                    incident.ActionTaken = actionTaken;
                    incident.ResolvedByUserId = userId;
                    incident.ResolvedAtUtc = now;
                    incident.UpdatedAtUtc = now;

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    await _auditLogService.LogAsync(
                        "StockIncident",
                        incident.Id.ToString(),
                        "Resolve",
                        userId,
                        null,
                        actionTaken,
                        $"Menyelesaikan/memposting kejadian {incident.IncidentNumber}: {actionTaken}",
                        cancellationToken);

                    return Result.Success();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure($"Gagal menyelesaikan kejadian: {ex.Message}");
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
        var incident = await _dbContext.StockIncidents.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident == null) return Result.Failure("Kejadian tidak ditemukan.");

        if (incident.Status == StockIncidentStatus.Resolved)
            return Result.Failure("Kejadian yang sudah diselesaikan/diposting tidak dapat dibatalkan.");

        incident.Status = StockIncidentStatus.Cancelled;
        incident.RejectionReason = reason;
        incident.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockIncident",
            incident.Id.ToString(),
            "Cancel",
            userId,
            null,
            reason,
            $"Membatalkan laporan kejadian {incident.IncidentNumber}: {reason}",
            cancellationToken);

        return Result.Success();
    }

    private static StockIncidentDto MapToDto(StockIncident i)
    {
        return new StockIncidentDto
        {
            Id = i.Id,
            IncidentNumber = i.IncidentNumber,
            IncidentDate = i.IncidentDate,
            ReportedAtUtc = i.ReportedAtUtc,
            WarehouseId = i.WarehouseId,
            WarehouseName = i.Warehouse?.Name ?? string.Empty,
            ProductId = i.ProductId,
            ProductSku = i.Product?.Sku ?? string.Empty,
            ProductName = i.Product?.Name ?? string.Empty,
            UnitName = i.Product?.Unit?.Name ?? string.Empty,
            LocationId = i.LocationId,
            LocationName = i.Location?.Name,
            Category = i.Category,
            Quantity = i.Quantity,
            EstimatedValue = i.EstimatedValue,
            Reason = i.Reason,
            InvestigationNotes = i.InvestigationNotes,
            ActionTaken = i.ActionTaken,
            Status = i.Status,
            ReportedByUserId = i.ReportedByUserId,
            ReportedByUserName = i.ReportedByUser?.FullName ?? i.ReportedByUser?.UserName ?? string.Empty,
            InvestigatedByUserId = i.InvestigatedByUserId,
            InvestigatedByUserName = i.InvestigatedByUser?.FullName ?? i.InvestigatedByUser?.UserName,
            ApprovedByUserId = i.ApprovedByUserId,
            ApprovedByUserName = i.ApprovedByUser?.FullName ?? i.ApprovedByUser?.UserName,
            ApprovedAtUtc = i.ApprovedAtUtc,
            RejectionReason = i.RejectionReason,
            ResolvedByUserId = i.ResolvedByUserId,
            ResolvedByUserName = i.ResolvedByUser?.FullName ?? i.ResolvedByUser?.UserName,
            ResolvedAtUtc = i.ResolvedAtUtc,
            CreatedAtUtc = i.CreatedAtUtc,
            ConcurrencyToken = i.ConcurrencyToken
        };
    }
}
