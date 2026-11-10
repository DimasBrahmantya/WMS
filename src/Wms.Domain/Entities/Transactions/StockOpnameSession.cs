using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Transactions;

public class StockOpnameSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionNumber { get; set; } = string.Empty; // OPN-YYYYMM-NNNNNN
    public DateTime CutoffDate { get; set; } = DateTime.UtcNow;

    public Guid WarehouseId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? LocationId { get; set; }
    public string? Notes { get; set; }

    public StockOpnameStatus Status { get; set; } = StockOpnameStatus.Draft;

    public decimal TotalVarianceQuantity { get; set; }
    public decimal TotalVarianceValue { get; set; }

    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? RejectionReason { get; set; }

    public Guid? PostedByUserId { get; set; }
    public DateTime? PostedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

    public Warehouse Warehouse { get; set; } = null!;
    public Category? Category { get; set; }
    public WarehouseLocation? Location { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? ApprovedByUser { get; set; }
    public ApplicationUser? PostedByUser { get; set; }

    public ICollection<StockOpnameLine> Lines { get; set; } = new List<StockOpnameLine>();
}
