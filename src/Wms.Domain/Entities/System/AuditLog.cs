using Wms.Domain.Common;

namespace Wms.Domain.Entities.System;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty; // e.g. "Create", "Update", "Delete", "Login", "Post", "Reverse"
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON
    public string? AffectedColumns { get; set; } // Comma separated or JSON
    public string? Reason { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
}
