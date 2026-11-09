namespace Wms.Application.Features.Identity.Users;

public record UserDetailDto(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    string? PhoneNumber,
    bool IsActive,
    DateTime CreatedAtUtc,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> WarehouseIds,
    IReadOnlyList<string> WarehouseNames);

public class GetUsersQuery : Common.Models.PaginationQuery
{
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

public record CreateUserCommand(
    string UserName,
    string Email,
    string FullName,
    string? PhoneNumber,
    string Password,
    List<string> Roles,
    List<Guid> WarehouseIds);

public record UpdateUserCommand(
    Guid Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    bool IsActive,
    List<string> Roles,
    List<Guid> WarehouseIds);

public record ChangeUserPasswordCommand(
    Guid Id,
    string NewPassword);

