namespace Wms.Domain.Common;

public interface IUserContext
{
    Guid? UserId { get; }
    string? Username { get; }
    string? FullName { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions { get; }
    IReadOnlyList<Guid> AllowedWarehouseIds { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permission);
    bool CanAccessWarehouse(Guid warehouseId);
}
