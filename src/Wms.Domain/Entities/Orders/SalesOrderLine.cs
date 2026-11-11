using Wms.Domain.Entities.Master;

namespace Wms.Domain.Entities.Orders;

public class SalesOrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SalesOrderId { get; set; }
    public Guid ProductId { get; set; }

    public decimal OrderedQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal DispatchedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal => OrderedQuantity * UnitPrice;

    public string? Notes { get; set; }

    public SalesOrder SalesOrder { get; set; } = null!;
    public Product Product { get; set; } = null!;

    public decimal RemainingQuantity => Math.Max(0, OrderedQuantity - DispatchedQuantity);
}

