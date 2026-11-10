using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Transactions;

public class StockTransfer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TransferNumber { get; set; } = string.Empty; // TRF-YYYYMM-NNNNNN
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;

    public Guid OriginWarehouseId { get; set; }
    public Guid DestinationWarehouseId { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }

    public StockTransferStatus Status { get; set; } = StockTransferStatus.Draft;

    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? RejectionReason { get; set; }

    public Guid? DispatchedByUserId { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }

    public Guid? ReceivedByUserId { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

    public Warehouse OriginWarehouse { get; set; } = null!;
    public Warehouse DestinationWarehouse { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? ApprovedByUser { get; set; }
    public ApplicationUser? DispatchedByUser { get; set; }
    public ApplicationUser? ReceivedByUser { get; set; }

    public ICollection<StockTransferLine> Lines { get; set; } = new List<StockTransferLine>();
}
