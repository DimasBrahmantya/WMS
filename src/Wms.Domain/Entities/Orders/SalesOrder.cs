using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Orders;

public class SalesOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNumber { get; set; } = string.Empty; // SO-YYYYMM-NNNNNN
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveryDate { get; set; }

    public Guid CustomerId { get; set; }
    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }

    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;
    public decimal TotalAmount { get; set; }

    public Guid CreatedByUserId { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public Guid? DispatchedByUserId { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }
    public string? CancellationReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

    public Customer Customer { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? ConfirmedByUser { get; set; }
    public ApplicationUser? DispatchedByUser { get; set; }

    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
}

