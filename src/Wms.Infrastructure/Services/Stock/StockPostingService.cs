using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Application.Features.Stock.Idempotency;
using Wms.Application.Features.Stock.Postings;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Stock;

public class StockPostingService : IStockPostingService
{
    private readonly IWmsDbContext _dbContext;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IIdempotencyService _idempotencyService;

    public StockPostingService(
        IWmsDbContext dbContext,
        IDocumentSequenceService sequenceService,
        IIdempotencyService idempotencyService)
    {
        _dbContext = dbContext;
        _sequenceService = sequenceService;
        _idempotencyService = idempotencyService;
    }

    public async Task<Result<Guid>> PostGoodsReceiptAsync(
        Guid receiptId,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _idempotencyService.ExecuteAsync(
            userId,
            nameof(PostGoodsReceiptAsync),
            idempotencyKey,
            async () =>
            {
                using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
                try
                {
                    var receipt = await _dbContext.GoodsReceipts
                        .Include(r => r.Lines)
                        .FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);

                    if (receipt == null)
                    {
                        return Result<Guid>.Failure("Dokumen penerimaan barang tidak ditemukan.");
                    }

                    if (receipt.Status != DocumentStatus.Draft && receipt.Status != DocumentStatus.PendingApproval)
                    {
                        return Result<Guid>.Failure($"Dokumen penerimaan barang tidak dapat diposting karena status saat ini adalah '{receipt.Status}'.");
                    }

                    if (receipt.Lines.Count == 0)
                    {
                        return Result<Guid>.Failure("Dokumen penerimaan barang tidak memiliki baris barang.");
                    }

                    // Urutkan deterministik untuk mencegah deadlock: urut ProductId kemudian WarehouseLocationId
                    var orderedLines = receipt.Lines
                        .OrderBy(l => l.ProductId)
                        .ThenBy(l => l.WarehouseLocationId)
                        .ToList();

                    var now = DateTime.UtcNow;

                    foreach (var line in orderedLines)
                    {
                        // 1. Ambil atau buat InventoryBalance
                        var balance = await _dbContext.InventoryBalances
                            .FirstOrDefaultAsync(b =>
                                b.ProductId == line.ProductId &&
                                b.WarehouseId == receipt.WarehouseId &&
                                b.WarehouseLocationId == line.WarehouseLocationId &&
                                b.Status == StockStatus.Available &&
                                b.BatchNumber == line.BatchNumber, cancellationToken);

                        var balanceBefore = balance?.OnHand ?? 0m;
                        var balanceAfter = balanceBefore + line.Quantity;

                        if (balance == null)
                        {
                            balance = new InventoryBalance
                            {
                                Id = Guid.NewGuid(),
                                ProductId = line.ProductId,
                                WarehouseId = receipt.WarehouseId,
                                WarehouseLocationId = line.WarehouseLocationId,
                                BatchNumber = line.BatchNumber,
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

                        // 2. Valuasi Rata-Rata Bergerak (Moving Average) per Barang per Gudang sesuai Bagian 9.3
                        var valuation = await _dbContext.InventoryValuations
                            .FirstOrDefaultAsync(v =>
                                v.ProductId == line.ProductId &&
                                v.WarehouseId == receipt.WarehouseId, cancellationToken);

                        if (valuation == null)
                        {
                            var totalLineVal = Math.Round(line.Quantity * line.UnitCost, 4, MidpointRounding.AwayFromZero);
                            valuation = new InventoryValuation
                            {
                                Id = Guid.NewGuid(),
                                ProductId = line.ProductId,
                                WarehouseId = receipt.WarehouseId,
                                OnHandQty = line.Quantity,
                                OnHandValue = totalLineVal,
                                AvgCost = line.UnitCost,
                                LastCost = line.UnitCost,
                                UpdatedAtUtc = now
                            };
                            _dbContext.InventoryValuations.Add(valuation);
                        }
                        else
                        {
                            var newQty = valuation.OnHandQty + line.Quantity;
                            var incomingValue = Math.Round(line.Quantity * line.UnitCost, 4, MidpointRounding.AwayFromZero);
                            var newValue = valuation.OnHandValue + incomingValue;
                            var newAvgCost = newQty > 0
                                ? Math.Round(newValue / newQty, 4, MidpointRounding.AwayFromZero)
                                : line.UnitCost;

                            valuation.OnHandQty = newQty;
                            valuation.OnHandValue = newValue;
                            valuation.AvgCost = newAvgCost;
                            valuation.LastCost = line.UnitCost;
                            valuation.UpdatedAtUtc = now;
                        }

                        // 3. Catat append-only StockMovement
                        var movementNumber = await _sequenceService.GetNextDocumentNumberAsync("MV", now, cancellationToken);
                        var movement = new StockMovement
                        {
                            Id = Guid.NewGuid(),
                            MovementNumber = movementNumber,
                            MovementType = MovementType.GoodsReceipt,
                            ProductId = line.ProductId,
                            WarehouseId = receipt.WarehouseId,
                            WarehouseLocationId = line.WarehouseLocationId,
                            BatchNumber = line.BatchNumber,
                            StockStatus = StockStatus.Available,
                            Quantity = line.Quantity,
                            Direction = MovementDirection.In,
                            BalanceBefore = balanceBefore,
                            BalanceAfter = balanceAfter,
                            UnitCost = line.UnitCost,
                            TotalValue = Math.Round(line.Quantity * line.UnitCost, 4, MidpointRounding.AwayFromZero),
                            SourceDocumentType = SourceDocumentType.GoodsReceipt,
                            SourceDocumentId = receipt.Id,
                            SourceDocumentNumber = receipt.ReceiptNumber,
                            Reason = receipt.Notes,
                            UserId = userId,
                            TimestampUtc = now,
                            Status = MovementStatus.Posted
                        };
                        _dbContext.StockMovements.Add(movement);
                    }

                    receipt.Status = DocumentStatus.Posted;
                    receipt.PostedByUserId = userId;
                    receipt.PostedAtUtc = now;
                    receipt.UpdatedAtUtc = now;

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return Result<Guid>.Success(receipt.Id);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<Guid>.Failure($"Gagal memposting penerimaan barang: {ex.Message}");
                }
            },
            cancellationToken);
    }

    public async Task<Result<Guid>> ReverseGoodsReceiptAsync(
        Guid receiptId,
        Guid userId,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<Guid>.Failure("Alasan pembatalan penerimaan barang wajib diisi.");
        }

        return await _idempotencyService.ExecuteAsync(
            userId,
            nameof(ReverseGoodsReceiptAsync),
            idempotencyKey,
            async () =>
            {
                using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
                try
                {
                    var receipt = await _dbContext.GoodsReceipts
                        .Include(r => r.Lines)
                        .FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);

                    if (receipt == null)
                    {
                        return Result<Guid>.Failure("Dokumen penerimaan barang tidak ditemukan.");
                    }

                    if (receipt.Status != DocumentStatus.Posted)
                    {
                        return Result<Guid>.Failure($"Hanya dokumen berstatus 'Posted' yang dapat dibatalkan. Status saat ini: '{receipt.Status}'.");
                    }

                    // Ambil movement asli yang terposting dari dokumen ini
                    var originalMovements = await _dbContext.StockMovements
                        .Where(m => m.SourceDocumentId == receipt.Id &&
                                    m.SourceDocumentType == SourceDocumentType.GoodsReceipt &&
                                    m.Status == MovementStatus.Posted)
                        .OrderBy(m => m.ProductId)
                        .ThenBy(m => m.WarehouseLocationId)
                        .ToListAsync(cancellationToken);

                    var now = DateTime.UtcNow;

                    foreach (var orig in originalMovements)
                    {
                        // 1. Periksa dan kurangi saldo di InventoryBalance
                        var balance = await _dbContext.InventoryBalances
                            .FirstOrDefaultAsync(b =>
                                b.ProductId == orig.ProductId &&
                                b.WarehouseId == orig.WarehouseId &&
                                b.WarehouseLocationId == orig.WarehouseLocationId &&
                                b.Status == orig.StockStatus &&
                                b.BatchNumber == orig.BatchNumber, cancellationToken);

                        var currentOnHand = balance?.OnHand ?? 0m;
                        if (currentOnHand < orig.Quantity)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return Result<Guid>.Failure($"Pembatalan ditolak karena sisa stok fisik di lokasi tidak mencukupi untuk dikurangi. Stok saat ini: {currentOnHand}, yang akan ditarik: {orig.Quantity}.");
                        }

                        var balanceBefore = currentOnHand;
                        var balanceAfter = currentOnHand - orig.Quantity;
                        balance!.OnHand = balanceAfter;
                        balance.UpdatedAtUtc = now;

                        // 2. Valuasi Rata-Rata Bergerak: hitung ulang setelah pembatalan sesuai Bagian 9.3
                        var valuation = await _dbContext.InventoryValuations
                            .FirstOrDefaultAsync(v =>
                                v.ProductId == orig.ProductId &&
                                v.WarehouseId == orig.WarehouseId, cancellationToken);

                        if (valuation != null)
                        {
                            var newQty = valuation.OnHandQty - orig.Quantity;
                            if (newQty < 0)
                            {
                                await transaction.RollbackAsync(cancellationToken);
                                return Result<Guid>.Failure("Pembatalan ditolak karena menyebabkan total kuantitas persediaan di gudang menjadi negatif.");
                            }

                            var originalVal = Math.Round(orig.Quantity * orig.UnitCost, 4, MidpointRounding.AwayFromZero);
                            var newValue = valuation.OnHandValue - originalVal;

                            if (newQty == 0)
                            {
                                valuation.OnHandQty = 0m;
                                valuation.OnHandValue = 0m;
                            }
                            else
                            {
                                if (newValue < 0) newValue = 0m;
                                valuation.OnHandQty = newQty;
                                valuation.OnHandValue = newValue;
                                valuation.AvgCost = Math.Round(newValue / newQty, 4, MidpointRounding.AwayFromZero);
                            }
                            valuation.UpdatedAtUtc = now;
                        }

                        // 3. Buat Movement Lawan (Append-Only)
                        var reverseMovementNumber = await _sequenceService.GetNextDocumentNumberAsync("MV", now, cancellationToken);
                        var reverseMovement = new StockMovement
                        {
                            Id = Guid.NewGuid(),
                            MovementNumber = reverseMovementNumber,
                            MovementType = MovementType.GoodsReceiptReversal,
                            ProductId = orig.ProductId,
                            WarehouseId = orig.WarehouseId,
                            WarehouseLocationId = orig.WarehouseLocationId,
                            BatchNumber = orig.BatchNumber,
                            StockStatus = orig.StockStatus,
                            Quantity = orig.Quantity,
                            Direction = MovementDirection.Out,
                            BalanceBefore = balanceBefore,
                            BalanceAfter = balanceAfter,
                            UnitCost = orig.UnitCost,
                            TotalValue = orig.TotalValue,
                            SourceDocumentType = SourceDocumentType.GoodsReceipt,
                            SourceDocumentId = receipt.Id,
                            SourceDocumentNumber = receipt.ReceiptNumber,
                            Reason = reason,
                            UserId = userId,
                            TimestampUtc = now,
                            Status = MovementStatus.Posted
                        };
                        _dbContext.StockMovements.Add(reverseMovement);

                        // Tautkan movement asal ke pembatalnya
                        orig.Status = MovementStatus.Reversed;
                        orig.ReversedByMovementId = reverseMovement.Id;
                        orig.ReversedAtUtc = now;
                    }

                    receipt.Status = DocumentStatus.Reversed;
                    receipt.ReversedByUserId = userId;
                    receipt.ReversedAtUtc = now;
                    receipt.ReversalReason = reason;
                    receipt.UpdatedAtUtc = now;

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return Result<Guid>.Success(receipt.Id);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<Guid>.Failure($"Gagal membatalkan penerimaan barang: {ex.Message}");
                }
            },
            cancellationToken);
    }

    public async Task<Result<Guid>> PostStockOutAsync(
        Guid stockOutId,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _idempotencyService.ExecuteAsync(
            userId,
            nameof(PostStockOutAsync),
            idempotencyKey,
            async () =>
            {
                using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
                try
                {
                    var stockOut = await _dbContext.StockOuts
                        .Include(s => s.Lines)
                        .FirstOrDefaultAsync(s => s.Id == stockOutId, cancellationToken);

                    if (stockOut == null)
                    {
                        return Result<Guid>.Failure("Dokumen pengeluaran barang tidak ditemukan.");
                    }

                    if (stockOut.Status != DocumentStatus.Draft && stockOut.Status != DocumentStatus.PendingApproval)
                    {
                        return Result<Guid>.Failure($"Dokumen pengeluaran barang tidak dapat diposting karena status saat ini adalah '{stockOut.Status}'.");
                    }

                    if (stockOut.Lines.Count == 0)
                    {
                        return Result<Guid>.Failure("Dokumen pengeluaran barang tidak memiliki baris barang.");
                    }

                    // Urutkan deterministik untuk mencegah deadlock
                    var orderedLines = stockOut.Lines
                        .OrderBy(l => l.ProductId)
                        .ThenBy(l => l.WarehouseLocationId)
                        .ToList();

                    var now = DateTime.UtcNow;

                    foreach (var line in orderedLines)
                    {
                        // 1. Periksa ketersediaan stok
                        var balance = await _dbContext.InventoryBalances
                            .FirstOrDefaultAsync(b =>
                                b.ProductId == line.ProductId &&
                                b.WarehouseId == stockOut.WarehouseId &&
                                b.WarehouseLocationId == line.WarehouseLocationId &&
                                b.Status == StockStatus.Available &&
                                b.BatchNumber == line.BatchNumber, cancellationToken);

                        var availableStock = balance?.Available ?? 0m;
                        if (balance == null || availableStock < line.Quantity)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return Result<Guid>.Failure($"Stok tidak mencukupi untuk barang di lokasi terpilih. Tersedia: {availableStock}, Diminta: {line.Quantity}.");
                        }

                        var balanceBefore = balance.OnHand;
                        var balanceAfter = balanceBefore - line.Quantity;
                        balance.OnHand = balanceAfter;
                        balance.UpdatedAtUtc = now;

                        // 2. Valuasi Rata-Rata Bergerak: dinilai pada avg_cost saat posting sesuai Bagian 9.3
                        var valuation = await _dbContext.InventoryValuations
                            .FirstOrDefaultAsync(v =>
                                v.ProductId == line.ProductId &&
                                v.WarehouseId == stockOut.WarehouseId, cancellationToken);

                        var unitCost = valuation?.AvgCost ?? 0m;
                        if (unitCost <= 0 && valuation != null && valuation.LastCost > 0)
                        {
                            unitCost = valuation.LastCost;
                        }

                        var totalValue = Math.Round(line.Quantity * unitCost, 4, MidpointRounding.AwayFromZero);

                        line.UnitCost = unitCost;
                        line.TotalCost = totalValue;

                        if (valuation != null)
                        {
                            var newQty = valuation.OnHandQty - line.Quantity;
                            var newValue = valuation.OnHandValue - totalValue;

                            // Aturan Bagian 9.3: Saat on_hand_qty menjadi 0, on_hand_value harus 0 (selisih pembulatan dikoreksi)
                            if (newQty <= 0)
                            {
                                valuation.OnHandQty = 0m;
                                valuation.OnHandValue = 0m;
                            }
                            else
                            {
                                if (newValue < 0) newValue = 0m;
                                valuation.OnHandQty = newQty;
                                valuation.OnHandValue = newValue;
                                // avg_cost tidak berubah pada barang keluar normal
                            }
                            valuation.UpdatedAtUtc = now;
                        }

                        // 3. Catat append-only StockMovement
                        var movementNumber = await _sequenceService.GetNextDocumentNumberAsync("MV", now, cancellationToken);
                        var movement = new StockMovement
                        {
                            Id = Guid.NewGuid(),
                            MovementNumber = movementNumber,
                            MovementType = MovementType.GoodsIssue,
                            ProductId = line.ProductId,
                            WarehouseId = stockOut.WarehouseId,
                            WarehouseLocationId = line.WarehouseLocationId,
                            BatchNumber = line.BatchNumber,
                            StockStatus = StockStatus.Available,
                            Quantity = line.Quantity,
                            Direction = MovementDirection.Out,
                            BalanceBefore = balanceBefore,
                            BalanceAfter = balanceAfter,
                            UnitCost = unitCost,
                            TotalValue = totalValue,
                            SourceDocumentType = SourceDocumentType.GoodsIssue,
                            SourceDocumentId = stockOut.Id,
                            SourceDocumentNumber = stockOut.IssueNumber,
                            Reason = stockOut.Notes ?? stockOut.IssueType.ToString(),
                            UserId = userId,
                            TimestampUtc = now,
                            Status = MovementStatus.Posted
                        };
                        _dbContext.StockMovements.Add(movement);
                    }

                    stockOut.Status = DocumentStatus.Posted;
                    stockOut.PostedByUserId = userId;
                    stockOut.PostedAtUtc = now;
                    stockOut.UpdatedAtUtc = now;

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return Result<Guid>.Success(stockOut.Id);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<Guid>.Failure($"Gagal memposting pengeluaran barang: {ex.Message}");
                }
            },
            cancellationToken);
    }

    public async Task<Result<Guid>> ReverseStockOutAsync(
        Guid stockOutId,
        Guid userId,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<Guid>.Failure("Alasan pembatalan pengeluaran barang wajib diisi.");
        }

        return await _idempotencyService.ExecuteAsync(
            userId,
            nameof(ReverseStockOutAsync),
            idempotencyKey,
            async () =>
            {
                using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
                try
                {
                    var stockOut = await _dbContext.StockOuts
                        .Include(s => s.Lines)
                        .FirstOrDefaultAsync(s => s.Id == stockOutId, cancellationToken);

                    if (stockOut == null)
                    {
                        return Result<Guid>.Failure("Dokumen pengeluaran barang tidak ditemukan.");
                    }

                    if (stockOut.Status != DocumentStatus.Posted)
                    {
                        return Result<Guid>.Failure($"Hanya dokumen berstatus 'Posted' yang dapat dibatalkan. Status saat ini: '{stockOut.Status}'.");
                    }

                    var originalMovements = await _dbContext.StockMovements
                        .Where(m => m.SourceDocumentId == stockOut.Id &&
                                    m.SourceDocumentType == SourceDocumentType.GoodsIssue &&
                                    m.Status == MovementStatus.Posted)
                        .OrderBy(m => m.ProductId)
                        .ThenBy(m => m.WarehouseLocationId)
                        .ToListAsync(cancellationToken);

                    var now = DateTime.UtcNow;

                    foreach (var orig in originalMovements)
                    {
                        // 1. Kembalikan stok ke InventoryBalance
                        var balance = await _dbContext.InventoryBalances
                            .FirstOrDefaultAsync(b =>
                                b.ProductId == orig.ProductId &&
                                b.WarehouseId == orig.WarehouseId &&
                                b.WarehouseLocationId == orig.WarehouseLocationId &&
                                b.Status == orig.StockStatus &&
                                b.BatchNumber == orig.BatchNumber, cancellationToken);

                        var balanceBefore = balance?.OnHand ?? 0m;
                        var balanceAfter = balanceBefore + orig.Quantity;

                        if (balance == null)
                        {
                            balance = new InventoryBalance
                            {
                                Id = Guid.NewGuid(),
                                ProductId = orig.ProductId,
                                WarehouseId = orig.WarehouseId,
                                WarehouseLocationId = orig.WarehouseLocationId,
                                BatchNumber = orig.BatchNumber,
                                Status = orig.StockStatus,
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

                        // 2. Valuasi Rata-Rata Bergerak: hitung ulang setelah pengembalian stok
                        var valuation = await _dbContext.InventoryValuations
                            .FirstOrDefaultAsync(v =>
                                v.ProductId == orig.ProductId &&
                                v.WarehouseId == orig.WarehouseId, cancellationToken);

                        if (valuation == null)
                        {
                            valuation = new InventoryValuation
                            {
                                Id = Guid.NewGuid(),
                                ProductId = orig.ProductId,
                                WarehouseId = orig.WarehouseId,
                                OnHandQty = orig.Quantity,
                                OnHandValue = orig.TotalValue,
                                AvgCost = orig.UnitCost,
                                LastCost = orig.UnitCost,
                                UpdatedAtUtc = now
                            };
                            _dbContext.InventoryValuations.Add(valuation);
                        }
                        else
                        {
                            var newQty = valuation.OnHandQty + orig.Quantity;
                            var returnedValue = Math.Round(orig.Quantity * orig.UnitCost, 4, MidpointRounding.AwayFromZero);
                            var newValue = valuation.OnHandValue + returnedValue;
                            var newAvgCost = newQty > 0
                                ? Math.Round(newValue / newQty, 4, MidpointRounding.AwayFromZero)
                                : orig.UnitCost;

                            valuation.OnHandQty = newQty;
                            valuation.OnHandValue = newValue;
                            valuation.AvgCost = newAvgCost;
                            valuation.UpdatedAtUtc = now;
                        }

                        // 3. Catat Movement Lawan (Append-Only)
                        var reverseMovementNumber = await _sequenceService.GetNextDocumentNumberAsync("MV", now, cancellationToken);
                        var reverseMovement = new StockMovement
                        {
                            Id = Guid.NewGuid(),
                            MovementNumber = reverseMovementNumber,
                            MovementType = MovementType.GoodsIssueReversal,
                            ProductId = orig.ProductId,
                            WarehouseId = orig.WarehouseId,
                            WarehouseLocationId = orig.WarehouseLocationId,
                            BatchNumber = orig.BatchNumber,
                            StockStatus = orig.StockStatus,
                            Quantity = orig.Quantity,
                            Direction = MovementDirection.In,
                            BalanceBefore = balanceBefore,
                            BalanceAfter = balanceAfter,
                            UnitCost = orig.UnitCost,
                            TotalValue = orig.TotalValue,
                            SourceDocumentType = SourceDocumentType.GoodsIssue,
                            SourceDocumentId = stockOut.Id,
                            SourceDocumentNumber = stockOut.IssueNumber,
                            Reason = reason,
                            UserId = userId,
                            TimestampUtc = now,
                            Status = MovementStatus.Posted
                        };
                        _dbContext.StockMovements.Add(reverseMovement);

                        orig.Status = MovementStatus.Reversed;
                        orig.ReversedByMovementId = reverseMovement.Id;
                        orig.ReversedAtUtc = now;
                    }

                    stockOut.Status = DocumentStatus.Reversed;
                    stockOut.ReversedByUserId = userId;
                    stockOut.ReversedAtUtc = now;
                    stockOut.ReversalReason = reason;
                    stockOut.UpdatedAtUtc = now;

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return Result<Guid>.Success(stockOut.Id);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<Guid>.Failure($"Gagal membatalkan pengeluaran barang: {ex.Message}");
                }
            },
            cancellationToken);
    }
}

