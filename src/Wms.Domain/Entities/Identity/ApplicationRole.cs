using Microsoft.AspNetCore.Identity;
using Wms.Domain.Common;

namespace Wms.Domain.Entities.Identity;

public class ApplicationRole : IdentityRole<Guid>, ISoftDeletable
{
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual ICollection<ApplicationUserRole> UserRoles { get; set; } = new List<ApplicationUserRole>();
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
