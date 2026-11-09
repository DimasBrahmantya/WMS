using Wms.Domain.Common;

namespace Wms.Domain.Entities.Master;

public class Warehouse : AuditableEntity, ISoftDeletable
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<WarehouseLocation> Locations { get; set; } = new List<WarehouseLocation>();
}
