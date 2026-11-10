using Wms.Domain.Entities.Master;

namespace Wms.Domain.Entities.Transactions;

public class StockIncidentLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockIncidentId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? LocationId { get; set; }

    public decimal Quantity { get; set; }
    public decimal EstimatedValue { get; set; }
    public string? Notes { get; set; }

    public StockIncident StockIncident { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public WarehouseLocation? Location { get; set; }
}
