using Microsoft.AspNetCore.Identity;
using Wms.Domain.Common;

namespace Wms.Domain.Entities.Identity;

public class ApplicationUser : IdentityUser<Guid>, ISoftDeletable
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public virtual ICollection<ApplicationUserRole> UserRoles { get; set; } = new List<ApplicationUserRole>();
    public virtual ICollection<UserWarehouse> UserWarehouses { get; set; } = new List<UserWarehouse>();
}
