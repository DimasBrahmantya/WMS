using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Transactions;

public class StockReturnLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockReturnId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? LocationId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public StockStatus TargetStockStatus { get; set; } = StockStatus.Available;

    public string? Notes { get; set; }

    public StockReturn StockReturn { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public WarehouseLocation? Location { get; set; }
}
