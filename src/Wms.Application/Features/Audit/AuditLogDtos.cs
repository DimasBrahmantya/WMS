using Wms.Application.Common.Models;

namespace Wms.Application.Features.Audit;

public class AuditLogQuery : PaginationQuery
{
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? AffectedColumns { get; set; }
    public string? Reason { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
}

public class AuditLogSummaryDto
{
    public int TotalLogs { get; set; }
    public int TotalCreates { get; set; }
    public int TotalUpdates { get; set; }
    public int TotalDeletes { get; set; }
    public int TotalPostings { get; set; }
    public int TotalReversals { get; set; }
    public int TotalLogins { get; set; }
    public DateTime? EarliestLogUtc { get; set; }
    public DateTime? LatestLogUtc { get; set; }
}

