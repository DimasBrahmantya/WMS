using Wms.Domain.Entities.Master;

namespace Wms.Domain.Entities.Transactions;

public class PurchaseOrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseOrderId { get; set; }
    public Guid ProductId { get; set; }

    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal => OrderedQuantity * UnitPrice;

    public string? Notes { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public Product Product { get; set; } = null!;

    public decimal RemainingQuantity => Math.Max(0, OrderedQuantity - ReceivedQuantity);
}
