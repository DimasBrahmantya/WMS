using Wms.Application.Common.Models;

namespace Wms.Application.Features.Identity.Users;

public interface IUserService
{
    Task<PaginatedList<UserDetailDto>> GetUsersAsync(GetUsersQuery query, CancellationToken cancellationToken = default);
    Task<Result<UserDetailDto>> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<UserDetailDto>> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateUserAsync(UpdateUserCommand command, CancellationToken cancellationToken = default);
    Task<Result> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> ToggleUserStatusAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> ResetPasswordAsync(ChangeUserPasswordCommand command, CancellationToken cancellationToken = default);
}

