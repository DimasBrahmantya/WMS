using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wms.Domain.Common;

namespace Wms.Infrastructure.Services;

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var idClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public string? Username => User?.Identity?.Name;

    public string? FullName => User?.FindFirst("full_name")?.Value ?? Username;

    public IReadOnlyList<string> Roles => User?.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToList()
        ?? (IReadOnlyList<string>)Array.Empty<string>();

    public IReadOnlyList<string> Permissions => User?.FindAll("permission").Select(c => c.Value).Distinct().ToList()
        ?? (IReadOnlyList<string>)Array.Empty<string>();

    public IReadOnlyList<Guid> AllowedWarehouseIds
    {
        get
        {
            var list = new List<Guid>();
            var claims = User?.FindAll("warehouse_id");
            if (claims != null)
            {
                foreach (var claim in claims)
                {
                    if (Guid.TryParse(claim.Value, out var wid))
                    {
                        list.Add(wid);
                    }
                }
            }
            return list;
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool HasPermission(string permission)
    {
        if (Roles.Contains("SuperAdmin"))
        {
            return true;
        }
        return Permissions.Contains(permission);
    }

    public bool CanAccessWarehouse(Guid warehouseId)
    {
        if (Roles.Contains("SuperAdmin"))
        {
            return true;
        }
        var allowed = AllowedWarehouseIds;
        return allowed.Count == 0 || allowed.Contains(warehouseId);
    }
}
