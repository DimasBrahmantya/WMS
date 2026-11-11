using Wms.Domain.Enums;

namespace Wms.Application.Features.Exports;

public record DirectExportRequest(
    string ReportType,
    ExportFormat Format,
    string Title,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? RequestedByUserId = null,
    string? RequestedByUserName = null
);

public record DirectExportResult(
    byte[] FileContents,
    string ContentType,
    string FileName,
    int RowCount
);

public record QueueExportJobRequest(
    string ReportType,
    ExportFormat Format,
    string FilterPayloadJson,
    string RequestedByUserId,
    string RequestedByUserName
);

public class ExportJobDto
{
    public Guid Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public ExportFormat Format { get; set; }
    public string FormatName => Format.ToString();
    public ExportJobStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? FileName { get; set; }
    public int RowCount { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string DownloadUrl => $"/api/exports/download/{Id}";
}
