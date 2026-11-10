using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Inventory;

public class InventoryBalance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public string? BatchNumber { get; set; }
    public StockStatus Status { get; set; } = StockStatus.Available;
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public decimal Available => OnHand - Reserved;

    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseLocation WarehouseLocation { get; set; } = null!;
}

