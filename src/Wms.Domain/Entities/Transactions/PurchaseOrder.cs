using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Transactions;

public class PurchaseOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PoNumber { get; set; } = string.Empty; // PO-YYYYMM-NNNNNN
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedDeliveryDate { get; set; }

    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public decimal TotalAmount { get; set; }

    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

    public Supplier Supplier { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? ApprovedByUser { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}
