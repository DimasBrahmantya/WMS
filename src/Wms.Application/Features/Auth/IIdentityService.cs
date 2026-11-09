using Wms.Application.Common.Models;

namespace Wms.Application.Features.Auth;

public interface IIdentityService
{
    Task<Result<UserDto>> AuthenticateAsync(LoginCommand command, CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetUserWarehouseIdsAsync(Guid userId, CancellationToken cancellationToken = default);
}
