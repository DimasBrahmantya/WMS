using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Transactions;

public class StockIncident
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IncidentNumber { get; set; } = string.Empty; // INC-YYYYMM-NNNNNN
    public DateTime IncidentDate { get; set; } = DateTime.UtcNow;
    public DateTime ReportedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? LocationId { get; set; }

    public StockIncidentCategory Category { get; set; } = StockIncidentCategory.Lost;
    public decimal Quantity { get; set; }
    public decimal EstimatedValue { get; set; }

    public string? Reason { get; set; }
    public string? InvestigationNotes { get; set; }
    public string? ActionTaken { get; set; }

    public StockIncidentStatus Status { get; set; } = StockIncidentStatus.Reported;

    public Guid ReportedByUserId { get; set; }
    public Guid? InvestigatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? RejectionReason { get; set; }

    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

    public Warehouse Warehouse { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public WarehouseLocation? Location { get; set; }
    public ApplicationUser ReportedByUser { get; set; } = null!;
    public ApplicationUser? InvestigatedByUser { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public ApplicationUser? ResolvedByUser { get; set; }

    public ICollection<StockIncidentLine> Lines { get; set; } = new List<StockIncidentLine>();
}
