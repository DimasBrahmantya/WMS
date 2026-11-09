using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Models;
using Wms.Application.Features.Identity.Users;
using Wms.Domain.Common;
using Wms.Domain.Entities.Identity;
using Wms.Infrastructure.Persistence;

namespace Wms.Infrastructure.Services.Identity;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly WmsDbContext _context;
    private readonly IUserContext _userContext;
    private readonly IValidator<CreateUserCommand> _createValidator;
    private readonly IValidator<UpdateUserCommand> _updateValidator;

    public UserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        WmsDbContext context,
        IUserContext userContext,
        IValidator<CreateUserCommand> createValidator,
        IValidator<UpdateUserCommand> updateValidator)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _userContext = userContext;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PaginatedList<UserDetailDto>> GetUsersAsync(GetUsersQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserWarehouses)
                .ThenInclude(uw => uw.Warehouse)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(u =>
                (u.UserName != null && u.UserName.ToLower().Contains(search)) ||
                (u.Email != null && u.Email.ToLower().Contains(search)) ||
                u.FullName.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            dbQuery = dbQuery.Where(u => u.UserRoles.Any(ur => ur.Role.Name == query.Role));
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(u => u.IsActive == query.IsActive.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        dbQuery = (query.SortBy?.ToLower(), query.SortDescending) switch
        {
            ("username", true) => dbQuery.OrderByDescending(u => u.UserName),
            ("username", false) => dbQuery.OrderBy(u => u.UserName),
            ("fullname", true) => dbQuery.OrderByDescending(u => u.FullName),
            ("fullname", false) => dbQuery.OrderBy(u => u.FullName),
            ("email", true) => dbQuery.OrderByDescending(u => u.Email),
            ("email", false) => dbQuery.OrderBy(u => u.Email),
            ("isactive", true) => dbQuery.OrderByDescending(u => u.IsActive),
            ("isactive", false) => dbQuery.OrderBy(u => u.IsActive),
            ("createdatutc", true) => dbQuery.OrderByDescending(u => u.CreatedAtUtc),
            ("createdatutc", false) => dbQuery.OrderBy(u => u.CreatedAtUtc),
            _ => dbQuery.OrderBy(u => u.FullName)
        };

        var users = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = users.Select(u => new UserDetailDto(
            u.Id,
            u.UserName ?? string.Empty,
            u.Email ?? string.Empty,
            u.FullName,
            u.PhoneNumber,
            u.IsActive,
            u.CreatedAtUtc,
            u.UserRoles.Select(ur => ur.Role.Name ?? string.Empty).ToList(),
            u.UserWarehouses.Select(uw => uw.WarehouseId).ToList(),
            u.UserWarehouses.Select(uw => uw.Warehouse?.Name ?? string.Empty).Where(name => !string.IsNullOrEmpty(name)).ToList()
        )).ToList();

        return new PaginatedList<UserDetailDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<Result<UserDetailDto>> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserWarehouses)
                .ThenInclude(uw => uw.Warehouse)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            return Result<UserDetailDto>.Failure("Pengguna tidak ditemukan.");
        }

        return Result<UserDetailDto>.Success(new UserDetailDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.FullName,
            user.PhoneNumber,
            user.IsActive,
            user.CreatedAtUtc,
            user.UserRoles.Select(ur => ur.Role.Name ?? string.Empty).ToList(),
            user.UserWarehouses.Select(uw => uw.WarehouseId).ToList(),
            user.UserWarehouses.Select(uw => uw.Warehouse?.Name ?? string.Empty).Where(name => !string.IsNullOrEmpty(name)).ToList()
        ));
    }

    public async Task<Result<UserDetailDto>> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<UserDetailDto>.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var normalizedUsername = command.UserName.Trim();
        var existingUser = await _userManager.FindByNameAsync(normalizedUsername);
        if (existingUser != null)
        {
            return Result<UserDetailDto>.Failure($"Username '{normalizedUsername}' sudah digunakan.");
        }

        var normalizedEmail = command.Email.Trim();
        var existingEmail = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingEmail != null)
        {
            return Result<UserDetailDto>.Failure($"Email '{normalizedEmail}' sudah digunakan.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedUsername,
            Email = normalizedEmail,
            FullName = command.FullName.Trim(),
            PhoneNumber = command.PhoneNumber?.Trim(),
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            return Result<UserDetailDto>.Failure(result.Errors.Select(e => e.Description));
        }

        if (command.Roles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, command.Roles);
        }

        if (command.WarehouseIds.Count > 0)
        {
            var userWarehouses = command.WarehouseIds.Distinct().Select(wid => new UserWarehouse
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                WarehouseId = wid
            });
            _context.UserWarehouses.AddRange(userWarehouses);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await GetUserByIdAsync(user.Id, cancellationToken);
    }

    public async Task<Result> UpdateUserAsync(UpdateUserCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var user = await _userManager.FindByIdAsync(command.Id.ToString());
        if (user == null)
        {
            return Result.Failure("Pengguna tidak ditemukan.");
        }

        var normalizedEmail = command.Email.Trim();
        var existingEmail = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingEmail != null && existingEmail.Id != user.Id)
        {
            return Result.Failure($"Email '{normalizedEmail}' sudah digunakan oleh pengguna lain.");
        }

        user.Email = normalizedEmail;
        user.FullName = command.FullName.Trim();
        user.PhoneNumber = command.PhoneNumber?.Trim();
        user.IsActive = command.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result.Failure(updateResult.Errors.Select(e => e.Description));
        }

        // Roles update
        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(command.Roles).ToList();
        var rolesToAdd = command.Roles.Except(currentRoles).ToList();

        if (rolesToRemove.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        if (rolesToAdd.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, rolesToAdd);
        }

        // Warehouses update
        var currentWarehouses = await _context.UserWarehouses
            .Where(uw => uw.UserId == user.Id)
            .ToListAsync(cancellationToken);

        _context.UserWarehouses.RemoveRange(currentWarehouses);

        if (command.WarehouseIds.Count > 0)
        {
            var newWarehouses = command.WarehouseIds.Distinct().Select(wid => new UserWarehouse
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                WarehouseId = wid
            });
            _context.UserWarehouses.AddRange(newWarehouses);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_userContext.UserId == id)
        {
            return Result.Failure("Anda tidak dapat menghapus akun Anda sendiri.");
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return Result.Failure("Pengguna tidak ditemukan.");
        }

        // Check if user is SuperAdmin and is the only SuperAdmin
        var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");
        if (isSuperAdmin)
        {
            var superAdminRole = await _roleManager.FindByNameAsync("SuperAdmin");
            if (superAdminRole != null)
            {
                var count = await _context.UserRoles.CountAsync(ur => ur.RoleId == superAdminRole.Id, cancellationToken);
                if (count <= 1)
                {
                    return Result.Failure("Tidak dapat menghapus SuperAdmin terakhir di sistem.");
                }
            }
        }

        // Check if user is referenced in audit logs or transactions
        var hasAuditLogs = await _context.AuditLogs.AnyAsync(a => a.UserId == id, cancellationToken);
        if (hasAuditLogs)
        {
            return Result.Failure("Pengguna tidak dapat dihapus permanen karena memiliki riwayat audit/aktivitas terkait. Silakan nonaktifkan akun ini.");
        }

        // Remove user warehouse associations first
        var userWarehouses = await _context.UserWarehouses.Where(uw => uw.UserId == id).ToListAsync(cancellationToken);
        _context.UserWarehouses.RemoveRange(userWarehouses);
        await _context.SaveChangesAsync(cancellationToken);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return Result.Failure(result.Errors.Select(e => e.Description));
        }

        return Result.Success();
    }

    public async Task<Result> ToggleUserStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_userContext.UserId == id)
        {
            return Result.Failure("Anda tidak dapat menonaktifkan akun Anda sendiri.");
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return Result.Failure("Pengguna tidak ditemukan.");
        }

        if (user.IsActive && await _userManager.IsInRoleAsync(user, "SuperAdmin"))
        {
            var superAdminRole = await _roleManager.FindByNameAsync("SuperAdmin");
            if (superAdminRole != null)
            {
                var activeSuperAdmins = await _context.Users
                    .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.RoleId == superAdminRole.Id))
                    .CountAsync(cancellationToken);

                if (activeSuperAdmins <= 1)
                {
                    return Result.Failure("Tidak dapat menonaktifkan SuperAdmin aktif terakhir.");
                }
            }
        }

        user.IsActive = !user.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return Result.Failure(result.Errors.Select(e => e.Description));
        }

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ChangeUserPasswordCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(command.Id.ToString());
        if (user == null)
        {
            return Result.Failure("Pengguna tidak ditemukan.");
        }

        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            return Result.Failure(removeResult.Errors.Select(e => e.Description));
        }

        var addResult = await _userManager.AddPasswordAsync(user, command.NewPassword);
        if (!addResult.Succeeded)
        {
            return Result.Failure(addResult.Errors.Select(e => e.Description));
        }

        return Result.Success();
    }
}

