using Wms.Domain.Common;

namespace Wms.Domain.Entities.Master;

public class Unit : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
