namespace Wms.Application.Features.Identity.Roles;

public record RoleDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int UserCount,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAtUtc);

public class GetRolesQuery : Common.Models.PaginationQuery
{
    public bool? IsActive { get; set; }
}

public record CreateRoleCommand(
    string Name,
    string? Description,
    List<string> Permissions);

public record UpdateRoleCommand(
    Guid Id,
    string? Description,
    bool IsActive,
    List<string> Permissions);

