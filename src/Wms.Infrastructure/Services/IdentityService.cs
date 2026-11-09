using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Models;
using Wms.Application.Features.Auth;
using Wms.Domain.Entities.Identity;
using Wms.Infrastructure.Persistence;

namespace Wms.Infrastructure.Services;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly WmsDbContext _dbContext;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        WmsDbContext dbContext)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
    }

    public async Task<Result<UserDto>> AuthenticateAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(command.EmailOrUsername)
                   ?? await _userManager.FindByNameAsync(command.EmailOrUsername);

        if (user == null || !user.IsActive)
        {
            // Do not disclose account existence
            return Result<UserDto>.Failure("Username, email, atau password tidak valid.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return Result<UserDto>.Failure("Akun terkunci sementara karena terlalu banyak percobaan gagal. Silakan coba lagi nanti.");
        }

        if (!result.Succeeded)
        {
            return Result<UserDto>.Failure("Username, email, atau password tidak valid.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user.Id, cancellationToken);
        var warehouses = await GetUserWarehouseIdsAsync(user.Id, cancellationToken);

        var dto = new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            IsActive = user.IsActive,
            Roles = roles.ToList(),
            Permissions = permissions,
            AllowedWarehouseIds = warehouses
        };

        return Result<UserDto>.Success(dto);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user.Id, cancellationToken);
        var warehouses = await GetUserWarehouseIdsAsync(user.Id, cancellationToken);

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            IsActive = user.IsActive,
            Roles = roles.ToList(),
            Permissions = permissions,
            AllowedWarehouseIds = warehouses
        };
    }

    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var roleIds = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        var permissions = await _dbContext.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission)
            .Distinct()
            .ToListAsync(cancellationToken);

        return permissions;
    }

    public async Task<IReadOnlyList<Guid>> GetUserWarehouseIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserWarehouses
            .Where(uw => uw.UserId == userId)
            .Select(uw => uw.WarehouseId)
            .ToListAsync(cancellationToken);
    }
}
