using Wms.Domain.Entities.Master;

namespace Wms.Domain.Entities.Transactions;

public class StockOpnameLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockOpnameSessionId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? LocationId { get; set; }

    public decimal SystemSnapshotQuantity { get; set; }
    public decimal SystemSnapshotAvgCost { get; set; }

    public decimal? CountedQuantity { get; set; }
    public decimal? RecountQuantity { get; set; }

    public decimal FinalCountQuantity => RecountQuantity ?? CountedQuantity ?? SystemSnapshotQuantity;
    public decimal VarianceQuantity => FinalCountQuantity - SystemSnapshotQuantity;
    public decimal VarianceValue => VarianceQuantity * SystemSnapshotAvgCost;

    public string? Notes { get; set; }

    public StockOpnameSession StockOpnameSession { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public WarehouseLocation? Location { get; set; }
}

