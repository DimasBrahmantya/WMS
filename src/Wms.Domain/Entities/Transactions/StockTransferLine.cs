using Wms.Domain.Entities.Master;

namespace Wms.Domain.Entities.Transactions;

public class StockTransferLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockTransferId { get; set; }
    public Guid ProductId { get; set; }

    public Guid? OriginLocationId { get; set; }
    public Guid? DestinationLocationId { get; set; }

    public decimal QuantityRequested { get; set; }
    public decimal QuantityDispatched { get; set; }
    public decimal QuantityReceived { get; set; }

    public string? Notes { get; set; }

    public StockTransfer StockTransfer { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public WarehouseLocation? OriginLocation { get; set; }
    public WarehouseLocation? DestinationLocation { get; set; }

    public decimal RemainingQuantity => Math.Max(0, QuantityDispatched - QuantityReceived);
}
