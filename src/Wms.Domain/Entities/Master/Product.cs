using Wms.Domain.Common;
using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Master;

public class Product : AuditableEntity, ISoftDeletable
{
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Brand { get; set; }

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public decimal MinStock { get; set; } = 0;
    public decimal? MaxStock { get; set; }

    public TrackingMethod TrackingMethod { get; set; } = TrackingMethod.None;
    public bool IsActive { get; set; } = true;
}
