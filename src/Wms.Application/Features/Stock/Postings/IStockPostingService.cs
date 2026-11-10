using Wms.Application.Common.Models;

namespace Wms.Application.Features.Stock.Postings;

public interface IStockPostingService
{
    /// <summary>
    /// Memposting penerimaan barang masuk (Goods Receipt):
    /// Menambah saldo on_hand, menghitung valuasi rata-rata bergerak (moving average),
    /// mencatat stock_movement append-only, dan mengubah status dokumen menjadi Posted.
    /// Operasi ini idempoten via idempotencyKey.
    /// </summary>
    Task<Result<Guid>> PostGoodsReceiptAsync(
        Guid receiptId,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Membatalkan penerimaan barang masuk yang telah diposting:
    /// Mengurangi saldo on_hand (menolak jika stok menjadi negatif),
    /// menghitung ulang valuasi rata-rata bergerak, mencatat stock_movement lawan bertaut,
    /// dan mengubah status dokumen menjadi Reversed dengan alasan pembatalan.
    /// </summary>
    Task<Result<Guid>> ReverseGoodsReceiptAsync(
        Guid receiptId,
        Guid userId,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Memposting pengeluaran barang keluar (Stock Out / Goods Issue):
    /// Memvalidasi ketersediaan stok, mengurangi saldo on_hand,
    /// menilai pengeluaran pada rata-rata bergerak saat posting,
    /// mencatat stock_movement append-only, dan mengubah status dokumen menjadi Posted.
    /// </summary>
    Task<Result<Guid>> PostStockOutAsync(
        Guid stockOutId,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Membatalkan pengeluaran barang keluar yang telah diposting:
    /// Mengembalikan stok ke saldo on_hand, menghitung ulang valuasi rata-rata bergerak,
    /// mencatat stock_movement lawan bertaut, dan mengubah status dokumen menjadi Reversed.
    /// </summary>
    Task<Result<Guid>> ReverseStockOutAsync(
        Guid stockOutId,
        Guid userId,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

