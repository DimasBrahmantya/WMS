using Wms.Application.Common.Models;

namespace Wms.Application.Features.Identity.Roles;

public interface IRoleService
{
    Task<PaginatedList<RoleDetailDto>> GetRolesAsync(GetRolesQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleDetailDto>> GetActiveRolesAsync(CancellationToken cancellationToken = default);
    Task<Result<RoleDetailDto>> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<RoleDetailDto>> CreateRoleAsync(CreateRoleCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateRoleAsync(UpdateRoleCommand command, CancellationToken cancellationToken = default);
    Task<Result> DeleteRoleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> ToggleRoleStatusAsync(Guid id, CancellationToken cancellationToken = default);
}

