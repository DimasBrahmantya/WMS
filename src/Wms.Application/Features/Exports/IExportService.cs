using Wms.Application.Common.Models;

namespace Wms.Application.Features.Exports;

public interface IExportService
{
    Task<Result<DirectExportResult>> ExportDirectAsync(DirectExportRequest request, CancellationToken cancellationToken = default);
    Task<Result<Guid>> QueueExportJobAsync(QueueExportJobRequest request, CancellationToken cancellationToken = default);
    Task<ExportJobDto?> GetExportJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> GetExportFileAsync(Guid jobId, string userId, bool isSuperAdmin, CancellationToken cancellationToken = default);
    Task<Result> ProcessExportJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<Result> CleanupExpiredExportsAsync(CancellationToken cancellationToken = default);
}

