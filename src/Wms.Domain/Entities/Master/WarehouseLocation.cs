using Wms.Domain.Common;

namespace Wms.Domain.Entities.Master;

public class WarehouseLocation : AuditableEntity, ISoftDeletable
{
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public string? Aisle { get; set; }
    public string? Rack { get; set; }
    public string? Bin { get; set; }
    public bool IsActive { get; set; } = true;
}
