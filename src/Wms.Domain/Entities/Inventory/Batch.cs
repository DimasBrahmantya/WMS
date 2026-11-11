using Wms.Domain.Common;
using Wms.Domain.Entities.Master;

namespace Wms.Domain.Entities.Inventory;

public class Batch : AuditableEntity
{
    public Guid ProductId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }

    public Product Product { get; set; } = null!;

    public bool IsExpired(DateOnly currentDate) => ExpiryDate.HasValue && ExpiryDate.Value < currentDate;

    public bool CanBeIssued(DateOnly currentDate) => !IsBlocked && !IsExpired(currentDate);
}

