using Wms.Application.Common.Models;
using Wms.Domain.Enums;

namespace Wms.Application.Features.Operations.Incidents;

public record StockIncidentDto
{
    public Guid Id { get; init; }
    public string IncidentNumber { get; init; } = string.Empty;
    public DateTime IncidentDate { get; init; }
    public DateTime ReportedAtUtc { get; init; }

    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = string.Empty;

    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;

    public Guid? LocationId { get; init; }
    public string? LocationName { get; init; }

    public StockIncidentCategory Category { get; init; }
    public decimal Quantity { get; init; }
    public decimal EstimatedValue { get; init; }

    public string? Reason { get; init; }
    public string? InvestigationNotes { get; init; }
    public string? ActionTaken { get; init; }
    public StockIncidentStatus Status { get; init; }

    public Guid ReportedByUserId { get; init; }
    public string ReportedByUserName { get; init; } = string.Empty;

    public Guid? InvestigatedByUserId { get; init; }
    public string? InvestigatedByUserName { get; init; }

    public Guid? ApprovedByUserId { get; init; }
    public string? ApprovedByUserName { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }
    public string? RejectionReason { get; init; }

    public Guid? ResolvedByUserId { get; init; }
    public string? ResolvedByUserName { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public string ConcurrencyToken { get; init; } = string.Empty;
}

public class CreateStockIncidentCommand
{
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? LocationId { get; set; }
    public StockIncidentCategory Category { get; set; }
    public decimal Quantity { get; set; }
    public string? Reason { get; set; }
    public string? InvestigationNotes { get; set; }
}

public class StockIncidentFilterQuery : PaginationQuery
{
    public string? SearchTerm { get; set; }
    public Guid? WarehouseId { get; set; }
    public StockIncidentCategory? Category { get; set; }
    public StockIncidentStatus? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
