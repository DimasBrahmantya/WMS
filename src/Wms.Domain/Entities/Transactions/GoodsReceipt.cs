using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Transactions;

public class GoodsReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReceiptNumber { get; set; } = string.Empty; // GR-YYYYMM-NNNNNN
    public DateTime DocumentDate { get; set; } = DateTime.UtcNow;

    public Guid WarehouseId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? PurchaseOrderNumber { get; set; }
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
    public Supplier? Supplier { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? PostedByUser { get; set; }
    public ApplicationUser? ReversedByUser { get; set; }

    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
}

