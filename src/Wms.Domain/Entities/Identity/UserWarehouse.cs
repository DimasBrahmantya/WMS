using Wms.Domain.Common;
using Wms.Domain.Entities.Master;

namespace Wms.Domain.Entities.Identity;

public class UserWarehouse : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid WarehouseId { get; set; }

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Warehouse? Warehouse { get; set; }
}
