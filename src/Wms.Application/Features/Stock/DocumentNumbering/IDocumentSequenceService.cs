namespace Wms.Application.Features.Stock.DocumentNumbering;

public interface IDocumentSequenceService
{
    /// <summary>
    /// Menghasilkan nomor dokumen sequential unik dengan format {PREFIX}-{YYYYMM}-{NNNNNN}.
    /// Dilakukan di dalam transaksi dan row-lock / atomic update untuk mencegah nomor ganda.
    /// </summary>
    Task<string> GetNextDocumentNumberAsync(string prefix, DateTime? date = null, CancellationToken cancellationToken = default);
}

