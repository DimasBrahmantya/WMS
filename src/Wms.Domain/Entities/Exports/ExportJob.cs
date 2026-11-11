using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Exports;

public class ExportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByUserName { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public ExportFormat Format { get; set; }
    public string FilterPayloadJson { get; set; } = "{}";
    public ExportJobStatus Status { get; set; } = ExportJobStatus.Pending;
    public string? FilePath { get; set; }
    public string? FileName { get; set; }
    public int RowCount { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(24);

    public void MarkProcessing()
    {
        Status = ExportJobStatus.Processing;
    }

    public void MarkCompleted(string filePath, string fileName, int rowCount, long fileSizeBytes, DateTime completedAtUtc)
    {
        Status = ExportJobStatus.Completed;
        FilePath = filePath;
        FileName = fileName;
        RowCount = rowCount;
        FileSizeBytes = fileSizeBytes;
        CompletedAtUtc = completedAtUtc;
    }

    public void MarkFailed(string errorMessage, DateTime failedAtUtc)
    {
        Status = ExportJobStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAtUtc = failedAtUtc;
    }

    public void MarkExpired()
    {
        Status = ExportJobStatus.Expired;
    }
}

