namespace Wms.Domain.Entities.System;

public class DocumentSequence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Prefix { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty; // e.g. "202611"
    public long LastNumber { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

