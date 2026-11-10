using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Guards;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Audit;
using Wms.Application.Features.Operations.Opname;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Application.Features.Stock.Idempotency;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Operations;

public class StockOpnameService : IStockOpnameService
{
    private readonly IWmsDbContext _dbContext;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IAppAuditService _auditLogService;

    public StockOpnameService(
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

    public async Task<Result<PaginatedList<StockOpnameSessionDto>>> GetSessionsAsync(
        StockOpnameFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = _dbContext.StockOpnameSessions
            .AsNoTracking()
            .Include(s => s.Warehouse)
            .Include(s => s.Category)
            .Include(s => s.Location)
            .Include(s => s.CreatedByUser)
            .Include(s => s.ApprovedByUser)
            .Include(s => s.PostedByUser)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(p => p.Unit)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Location)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            dbQuery = dbQuery.Where(s =>
                s.SessionNumber.ToLower().Contains(term) ||
                s.Warehouse.Name.ToLower().Contains(term) ||
                (s.Notes != null && s.Notes.ToLower().Contains(term)));
        }

        if (query.WarehouseId.HasValue)
            dbQuery = dbQuery.Where(s => s.WarehouseId == query.WarehouseId.Value);

        if (query.Status.HasValue)
            dbQuery = dbQuery.Where(s => s.Status == query.Status.Value);

        if (query.StartDate.HasValue)
            dbQuery = dbQuery.Where(s => s.CutoffDate >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            dbQuery = dbQuery.Where(s => s.CutoffDate <= query.EndDate.Value);

        dbQuery = dbQuery.OrderByDescending(s => s.CutoffDate).ThenByDescending(s => s.CreatedAtUtc);

        var totalCount = await dbQuery.CountAsync(cancellationToken);
        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => MapToDto(s))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<StockOpnameSessionDto>>.Success(
            new PaginatedList<StockOpnameSessionDto>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<StockOpnameSessionDto>> GetSessionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var s = await _dbContext.StockOpnameSessions
            .AsNoTracking()
            .Include(s => s.Warehouse)
            .Include(s => s.Category)
            .Include(s => s.Location)
            .Include(s => s.CreatedByUser)
            .Include(s => s.ApprovedByUser)
            .Include(s => s.PostedByUser)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(p => p.Unit)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Location)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (s == null)
            return Result<StockOpnameSessionDto>.Failure("Sesi Stock Opname tidak ditemukan.");

        return Result<StockOpnameSessionDto>.Success(MapToDto(s));
    }

    public async Task<Result<Guid>> CreateSessionAsync(
        CreateStockOpnameSessionCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var sessionNumber = await _sequenceService.GetNextDocumentNumberAsync("OPN", null, cancellationToken);

        var session = new StockOpnameSession
        {
            Id = Guid.NewGuid(),
            SessionNumber = sessionNumber,
            CutoffDate = DateTime.UtcNow,
            WarehouseId = command.WarehouseId,
            CategoryId = command.CategoryId,
            LocationId = command.LocationId,
            Notes = command.Notes,
            Status = StockOpnameStatus.Draft,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.StockOpnameSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockOpname",
            session.Id.ToString(),
            "Create",
            userId,
            null,
            JsonSerializer.Serialize(new { session.SessionNumber, session.WarehouseId }),
            $"Membuat sesi Stock Opname {session.SessionNumber}",
            cancellationToken);

        return Result<Guid>.Success(session.Id);
    }

    public async Task<Result> StartCountingAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.StockOpnameSessions
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (session == null) return Result.Failure("Sesi Stock Opname tidak ditemukan.");
        if (session.Status != StockOpnameStatus.Draft)
            return Result.Failure("Snapshot dan proses hitung hanya dapat dimulai dari sesi berstatus Draft.");

        session.CutoffDate = DateTime.UtcNow;

        // Ambil saldo sistem yang sesuai cakupan gudang/lokasi/kategori
        var balanceQuery = _dbContext.InventoryBalances
            .Include(b => b.Product)
            .Where(b => b.WarehouseId == session.WarehouseId && b.Status == StockStatus.Available);

        if (session.LocationId.HasValue)
            balanceQuery = balanceQuery.Where(b => b.WarehouseLocationId == session.LocationId.Value);

        if (session.CategoryId.HasValue)
            balanceQuery = balanceQuery.Where(b => b.Product.CategoryId == session.CategoryId.Value);

        var balances = await balanceQuery.ToListAsync(cancellationToken);

        // Ambil valuasi rata-rata per barang di gudang tersebut
        var productIds = balances.Select(b => b.ProductId).Distinct().ToList();
        var valuations = await _dbContext.InventoryValuations
            .Where(v => v.WarehouseId == session.WarehouseId && productIds.Contains(v.ProductId))
            .ToDictionaryAsync(v => v.ProductId, cancellationToken);

        foreach (var balance in balances)
        {
            var avgCost = valuations.TryGetValue(balance.ProductId, out var val) ? val.AvgCost : 0m;

            var line = new StockOpnameLine
            {
                Id = Guid.NewGuid(),
                StockOpnameSessionId = session.Id,
                ProductId = balance.ProductId,
                LocationId = balance.WarehouseLocationId,
                SystemSnapshotQuantity = balance.OnHand,
                SystemSnapshotAvgCost = avgCost,
                CountedQuantity = null,
                RecountQuantity = null,
                Notes = null
            };
            _dbContext.StockOpnameLines.Add(line);
        }

        session.Status = StockOpnameStatus.Counting;
        session.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockOpname",
            session.Id.ToString(),
            "StartCounting",
            userId,
            null,
            null,
            $"Mengambil snapshot saldo sistem ({session.Lines.Count} item) untuk sesi {session.SessionNumber}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RecordCountsAsync(
        Guid id,
        RecordOpnameCountCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.StockOpnameSessions
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (session == null) return Result.Failure("Sesi Stock Opname tidak ditemukan.");

        if (session.Status != StockOpnameStatus.Counting && session.Status != StockOpnameStatus.Review)
            return Result.Failure("Hasil hitung hanya dapat diinput pada sesi berstatus Counting atau Review.");

        foreach (var inputLine in command.Lines)
        {
            var line = session.Lines.FirstOrDefault(l => l.Id == inputLine.LineId);
            if (line != null)
            {
                if (inputLine.CountedQuantity.HasValue)
                    line.CountedQuantity = inputLine.CountedQuantity.Value;

                if (inputLine.RecountQuantity.HasValue)
                    line.RecountQuantity = inputLine.RecountQuantity.Value;

                if (!string.IsNullOrWhiteSpace(inputLine.Notes))
                    line.Notes = inputLine.Notes;
            }
        }

        session.TotalVarianceQuantity = session.Lines.Sum(l => l.VarianceQuantity);
        session.TotalVarianceValue = session.Lines.Sum(l => l.VarianceValue);
        session.Status = StockOpnameStatus.Review;
        session.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> SubmitForApprovalAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.StockOpnameSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session == null) return Result.Failure("Sesi Stock Opname tidak ditemukan.");

        if (session.Status != StockOpnameStatus.Review && session.Status != StockOpnameStatus.Counting)
            return Result.Failure("Hanya sesi yang sedang ditinjau yang dapat diajukan untuk approval.");

        session.Status = StockOpnameStatus.PendingApproval;
        session.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockOpname",
            session.Id.ToString(),
            "SubmitForApproval",
            userId,
            null,
            null,
            $"Mengajukan sesi Stock Opname {session.SessionNumber} untuk approval supervisor",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ApproveAsync(
        Guid id,
        Guid approverUserId,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.StockOpnameSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session == null) return Result.Failure("Sesi Stock Opname tidak ditemukan.");

        if (session.Status != StockOpnameStatus.PendingApproval)
            return Result.Failure("Hanya sesi berstatus PendingApproval yang dapat disetujui.");

        // Enforce Separation of Duties
        var sodResult = SeparationOfDutiesGuard.EnsureCanApprove(session.CreatedByUserId, approverUserId, session.SessionNumber);
        if (!sodResult.Succeeded)
            return sodResult;

        session.Status = StockOpnameStatus.Approved;
        session.ApprovedByUserId = approverUserId;
        session.ApprovedAtUtc = DateTime.UtcNow;
        session.RejectionReason = null;
        session.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockOpname",
            session.Id.ToString(),
            "Approve",
            approverUserId,
            null,
            null,
            $"Menyetujui sesi Stock Opname {session.SessionNumber}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RejectAsync(
        Guid id,
        Guid approverUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.StockOpnameSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session == null) return Result.Failure("Sesi Stock Opname tidak ditemukan.");

        if (session.Status != StockOpnameStatus.PendingApproval)
            return Result.Failure("Hanya sesi berstatus PendingApproval yang dapat ditolak.");

        var sodResult = SeparationOfDutiesGuard.EnsureCanApprove(session.CreatedByUserId, approverUserId, session.SessionNumber);
        if (!sodResult.Succeeded)
            return sodResult;

        session.Status = StockOpnameStatus.Rejected;
        session.RejectionReason = reason;
        session.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockOpname",
            session.Id.ToString(),
            "Reject",
            approverUserId,
            null,
            reason,
            $"Menolak sesi Stock Opname {session.SessionNumber}: {reason}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> PostAdjustmentAsync(
        Guid id,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _idempotencyService.ExecuteAsync(
            userId,
            nameof(PostAdjustmentAsync),
            idempotencyKey,
            async () =>
            {
                using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
                try
                {
                    var session = await _dbContext.StockOpnameSessions
                        .Include(s => s.Lines)
                        .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

                    if (session == null) return Result.Failure("Sesi Stock Opname tidak ditemukan.");
                    if (session.Status != StockOpnameStatus.Approved)
                        return Result.Failure("Hanya sesi Stock Opname yang telah disetujui (Approved) yang dapat diposting.");

                    var now = DateTime.UtcNow;

                    foreach (var line in session.Lines)
                    {
                        var varianceQty = line.VarianceQuantity;
                        if (varianceQty == 0) continue; // Tidak ada selisih, tidak perlu movement

                        var balance = await _dbContext.InventoryBalances
                            .FirstOrDefaultAsync(b => b.ProductId == line.ProductId &&
                                                      b.WarehouseId == session.WarehouseId &&
                                                      b.WarehouseLocationId == (line.LocationId ?? Guid.Empty) &&
                                                      b.Status == StockStatus.Available, cancellationToken);

                        var balanceBefore = balance?.OnHand ?? 0m;
                        var balanceAfter = balanceBefore + varianceQty;

                        if (balanceAfter < 0)
                        {
                            return Result.Failure($"Penyesuaian stok opname menghasilkan stok negatif ({balanceAfter}) untuk produk {line.ProductId}. Transaksi dibatalkan.");
                        }

                        if (balance == null)
                        {
                            balance = new InventoryBalance
                            {
                                Id = Guid.NewGuid(),
                                ProductId = line.ProductId,
                                WarehouseId = session.WarehouseId,
                                WarehouseLocationId = line.LocationId ?? Guid.Empty,
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

                        // Valuasi persediaan
                        var valuation = await _dbContext.InventoryValuations
                            .FirstOrDefaultAsync(v => v.ProductId == line.ProductId && v.WarehouseId == session.WarehouseId, cancellationToken);

                        var unitCost = line.SystemSnapshotAvgCost;
                        var varianceValue = Math.Round(Math.Abs(varianceQty) * unitCost, 4, MidpointRounding.AwayFromZero);

                        if (valuation != null)
                        {
                            valuation.OnHandQty += varianceQty;
                            if (varianceQty > 0)
                                valuation.OnHandValue += varianceValue;
                            else
                                valuation.OnHandValue = Math.Max(0, valuation.OnHandValue - varianceValue);

                            if (valuation.OnHandQty <= 0)
                            {
                                valuation.OnHandQty = 0;
                                valuation.OnHandValue = 0;
                            }
                            valuation.UpdatedAtUtc = now;
                        }

                        // Catat Stock Movement Penyesuaian
                        var movementNumber = await _sequenceService.GetNextDocumentNumberAsync("MOV", now, cancellationToken);
                        var movement = new StockMovement
                        {
                            Id = Guid.NewGuid(),
                            MovementNumber = movementNumber,
                            MovementType = MovementType.StockAdjustment,
                            ProductId = line.ProductId,
                            WarehouseId = session.WarehouseId,
                            WarehouseLocationId = line.LocationId ?? Guid.Empty,
                            StockStatus = StockStatus.Available,
                            Quantity = Math.Abs(varianceQty),
                            Direction = varianceQty > 0 ? MovementDirection.In : MovementDirection.Out,
                            BalanceBefore = balanceBefore,
                            BalanceAfter = balanceAfter,
                            UnitCost = unitCost,
                            TotalValue = varianceValue,
                            SourceDocumentType = SourceDocumentType.StockOpname,
                            SourceDocumentId = session.Id,
                            SourceDocumentNumber = session.SessionNumber,
                            Reason = $"Penyesuaian hasil opname: {(varianceQty > 0 ? "Kelebihan fisik" : "Kekurangan fisik")}. {line.Notes}",
                            UserId = userId,
                            TimestampUtc = now,
                            Status = MovementStatus.Posted
                        };
                        _dbContext.StockMovements.Add(movement);
                    }

                    session.Status = StockOpnameStatus.Posted;
                    session.PostedByUserId = userId;
                    session.PostedAtUtc = now;
                    session.UpdatedAtUtc = now;

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    await _auditLogService.LogAsync(
                        "StockOpname",
                        session.Id.ToString(),
                        "Post",
                        userId,
                        null,
                        null,
                        $"Memposting penyesuaian stok sesi Opname {session.SessionNumber} ke ledger",
                        cancellationToken);

                    return Result.Success();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure($"Gagal memposting penyesuaian opname: {ex.Message}");
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
        var session = await _dbContext.StockOpnameSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session == null) return Result.Failure("Sesi Stock Opname tidak ditemukan.");

        if (session.Status == StockOpnameStatus.Posted)
            return Result.Failure("Sesi yang sudah diposting ke ledger tidak dapat dibatalkan.");

        session.Status = StockOpnameStatus.Cancelled;
        session.RejectionReason = reason;
        session.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "StockOpname",
            session.Id.ToString(),
            "Cancel",
            userId,
            null,
            reason,
            $"Membatalkan sesi Opname {session.SessionNumber}: {reason}",
            cancellationToken);

        return Result.Success();
    }

    private static StockOpnameSessionDto MapToDto(StockOpnameSession s)
    {
        return new StockOpnameSessionDto
        {
            Id = s.Id,
            SessionNumber = s.SessionNumber,
            CutoffDate = s.CutoffDate,
            WarehouseId = s.WarehouseId,
            WarehouseName = s.Warehouse?.Name ?? string.Empty,
            CategoryId = s.CategoryId,
            CategoryName = s.Category?.Name,
            LocationId = s.LocationId,
            LocationName = s.Location?.Name,
            Notes = s.Notes,
            Status = s.Status,
            TotalVarianceQuantity = s.TotalVarianceQuantity,
            TotalVarianceValue = s.TotalVarianceValue,
            CreatedByUserId = s.CreatedByUserId,
            CreatedByUserName = s.CreatedByUser?.FullName ?? s.CreatedByUser?.UserName ?? string.Empty,
            ApprovedByUserId = s.ApprovedByUserId,
            ApprovedByUserName = s.ApprovedByUser?.FullName ?? s.ApprovedByUser?.UserName,
            ApprovedAtUtc = s.ApprovedAtUtc,
            RejectionReason = s.RejectionReason,
            PostedByUserId = s.PostedByUserId,
            PostedByUserName = s.PostedByUser?.FullName ?? s.PostedByUser?.UserName,
            PostedAtUtc = s.PostedAtUtc,
            CreatedAtUtc = s.CreatedAtUtc,
            ConcurrencyToken = s.ConcurrencyToken,
            Lines = s.Lines.Select(l => new StockOpnameLineDto
            {
                Id = l.Id,
                StockOpnameSessionId = l.StockOpnameSessionId,
                ProductId = l.ProductId,
                ProductSku = l.Product?.Sku ?? string.Empty,
                ProductName = l.Product?.Name ?? string.Empty,
                UnitName = l.Product?.Unit?.Name ?? string.Empty,
                LocationId = l.LocationId,
                LocationName = l.Location?.Name,
                SystemSnapshotQuantity = l.SystemSnapshotQuantity,
                SystemSnapshotAvgCost = l.SystemSnapshotAvgCost,
                CountedQuantity = l.CountedQuantity,
                RecountQuantity = l.RecountQuantity,
                FinalCountQuantity = l.FinalCountQuantity,
                VarianceQuantity = l.VarianceQuantity,
                VarianceValue = l.VarianceValue,
                Notes = l.Notes
            }).ToList()
        };
    }
}
