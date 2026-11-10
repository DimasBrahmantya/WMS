using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Transactions;

public class StockOut
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IssueNumber { get; set; } = string.Empty; // GI-YYYYMM-NNNNNN
    public DateTime DocumentDate { get; set; } = DateTime.UtcNow;

    public Guid WarehouseId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Recipient { get; set; }
    public StockOutType IssueType { get; set; } = StockOutType.CustomerDelivery;
    public string? Notes { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public Guid CreatedByUserId { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTime? PostedAtUtc { get; set; }

    public Guid? ReversedByUserId { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    public string? ReversalReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

    public Warehouse Warehouse { get; set; } = null!;
    public Customer? Customer { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? PostedByUser { get; set; }
    public ApplicationUser? ReversedByUser { get; set; }

    public ICollection<StockOutLine> Lines { get; set; } = new List<StockOutLine>();
}

