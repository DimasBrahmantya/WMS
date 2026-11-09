using Wms.Domain.Common;

namespace Wms.Domain.Entities.Identity;

public class RolePermission : BaseEntity
{
    public Guid RoleId { get; set; }
    public string Permission { get; set; } = string.Empty;

    public virtual ApplicationRole Role { get; set; } = null!;
}
