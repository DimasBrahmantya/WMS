namespace Wms.Application.Features.Backup;

public record BackupMetadataDto
{
    public string System { get; init; } = "WmsModularMonolith";
    public string Version { get; init; } = "1.0.0-Tahap7";
    public DateTime ExportedAtUtc { get; init; }
    public string ExportedBy { get; init; } = string.Empty;
    public string Sha256Checksum { get; init; } = string.Empty;
    public int TotalRecords { get; init; }
}

public record BackupSnapshotDto
{
    public BackupMetadataDto Metadata { get; set; } = new();
    public List<object> Products { get; init; } = new();
    public List<object> Categories { get; init; } = new();
    public List<object> Warehouses { get; init; } = new();
    public List<object> Locations { get; init; } = new();
    public List<object> Batches { get; init; } = new();
    public List<object> InventoryBalances { get; init; } = new();
    public List<object> StockMovements { get; init; } = new();
    public List<object> SalesOrders { get; init; } = new();
    public List<object> Suppliers { get; init; } = new();
    public List<object> Customers { get; init; } = new();
}

public record BackupResultDto(
    string FileName,
    byte[] Content,
    string ContentType,
    string ChecksumSha256,
    int RecordCount,
    DateTime ExportedAtUtc
);

