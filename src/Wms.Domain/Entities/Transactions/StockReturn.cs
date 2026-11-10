using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Transactions;

public class StockReturn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReturnNumber { get; set; } = string.Empty; // RET-YYYYMM-NNNNNN
    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

    public Guid WarehouseId { get; set; }
    public StockReturnType ReturnType { get; set; } = StockReturnType.ReturnToSupplier;

    public Guid? SupplierId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? ReferenceDocumentNumber { get; set; }

    public string? Reason { get; set; }
    public string? Notes { get; set; }

    public StockReturnStatus Status { get; set; } = StockReturnStatus.Draft;

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
    public Supplier? Supplier { get; set; }
    public Customer? Customer { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? ApprovedByUser { get; set; }
    public ApplicationUser? PostedByUser { get; set; }

    public ICollection<StockReturnLine> Lines { get; set; } = new List<StockReturnLine>();
}
