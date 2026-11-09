using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Models;
using Wms.Application.Features.Identity.Roles;
using Wms.Domain.Entities.Identity;
using Wms.Infrastructure.Persistence;

namespace Wms.Infrastructure.Services.Identity;

public class RoleService : IRoleService
{
    private static readonly HashSet<string> SystemRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "SuperAdmin", "AdminGudang", "PetugasGudang", "Supervisor", "Auditor"
    };

    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly WmsDbContext _context;
    private readonly IValidator<CreateRoleCommand> _createValidator;
    private readonly IValidator<UpdateRoleCommand> _updateValidator;

    public RoleService(
        RoleManager<ApplicationRole> roleManager,
        WmsDbContext context,
        IValidator<CreateRoleCommand> createValidator,
        IValidator<UpdateRoleCommand> updateValidator)
    {
        _roleManager = roleManager;
        _context = context;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PaginatedList<RoleDetailDto>> GetRolesAsync(GetRolesQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.Roles
            .AsNoTracking()
            .Include(r => r.UserRoles)
            .Include(r => r.RolePermissions)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(r =>
                (r.Name != null && r.Name.ToLower().Contains(search)) ||
                (r.Description != null && r.Description.ToLower().Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.IsActive == query.IsActive.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        dbQuery = (query.SortBy?.ToLower(), query.SortDescending) switch
        {
            ("name", true) => dbQuery.OrderByDescending(r => r.Name),
            ("name", false) => dbQuery.OrderBy(r => r.Name),
            ("isactive", true) => dbQuery.OrderByDescending(r => r.IsActive),
            ("isactive", false) => dbQuery.OrderBy(r => r.IsActive),
            ("createdatutc", true) => dbQuery.OrderByDescending(r => r.CreatedAtUtc),
            ("createdatutc", false) => dbQuery.OrderBy(r => r.CreatedAtUtc),
            _ => dbQuery.OrderBy(r => r.Name)
        };

        var roles = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = roles.Select(r => new RoleDetailDto(
            r.Id,
            r.Name ?? string.Empty,
            r.Description,
            r.IsActive,
            r.UserRoles.Count,
            r.RolePermissions.Select(rp => rp.Permission).ToList(),
            r.CreatedAtUtc
        )).ToList();

        return new PaginatedList<RoleDetailDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<RoleDetailDto>> GetActiveRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _context.Roles
            .AsNoTracking()
            .Include(r => r.UserRoles)
            .Include(r => r.RolePermissions)
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(r => new RoleDetailDto(
            r.Id,
            r.Name ?? string.Empty,
            r.Description,
            r.IsActive,
            r.UserRoles.Count,
            r.RolePermissions.Select(rp => rp.Permission).ToList(),
            r.CreatedAtUtc
        )).ToList();
    }

    public async Task<Result<RoleDetailDto>> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .AsNoTracking()
            .Include(r => r.UserRoles)
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (role == null)
        {
            return Result<RoleDetailDto>.Failure("Peran (role) tidak ditemukan.");
        }

        return Result<RoleDetailDto>.Success(new RoleDetailDto(
            role.Id,
            role.Name ?? string.Empty,
            role.Description,
            role.IsActive,
            role.UserRoles.Count,
            role.RolePermissions.Select(rp => rp.Permission).ToList(),
            role.CreatedAtUtc
        ));
    }

    public async Task<Result<RoleDetailDto>> CreateRoleAsync(CreateRoleCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<RoleDetailDto>.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var normalizedName = command.Name.Trim();
        var exists = await _roleManager.RoleExistsAsync(normalizedName);
        if (exists)
        {
            return Result<RoleDetailDto>.Failure($"Peran dengan nama '{normalizedName}' sudah ada.");
        }

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            NormalizedName = normalizedName.ToUpperInvariant(),
            Description = command.Description?.Trim() ?? string.Empty,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            return Result<RoleDetailDto>.Failure(result.Errors.Select(e => e.Description));
        }

        if (command.Permissions.Count > 0)
        {
            var permissions = command.Permissions.Distinct().Select(p => new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = role.Id,
                Permission = p
            });
            _context.RolePermissions.AddRange(permissions);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await GetRoleByIdAsync(role.Id, cancellationToken);
    }

    public async Task<Result> UpdateRoleAsync(UpdateRoleCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        var role = await _roleManager.FindByIdAsync(command.Id.ToString());
        if (role == null)
        {
            return Result.Failure("Peran (role) tidak ditemukan.");
        }

        if (role.Name != null && SystemRoles.Contains(role.Name) && !command.IsActive)
        {
            return Result.Failure($"Peran sistem bawaan '{role.Name}' tidak boleh dinonaktifkan.");
        }

        role.Description = command.Description?.Trim() ?? string.Empty;
        role.IsActive = command.IsActive;

        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            return Result.Failure(result.Errors.Select(e => e.Description));
        }

        // Update permissions
        var existingPerms = await _context.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync(cancellationToken);

        _context.RolePermissions.RemoveRange(existingPerms);

        if (command.Permissions.Count > 0)
        {
            var newPerms = command.Permissions.Distinct().Select(p => new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = role.Id,
                Permission = p
            });
            _context.RolePermissions.AddRange(newPerms);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteRoleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null)
        {
            return Result.Failure("Peran (role) tidak ditemukan.");
        }

        if (role.Name != null && SystemRoles.Contains(role.Name))
        {
            return Result.Failure($"Peran sistem bawaan '{role.Name}' tidak dapat dihapus.");
        }

        var hasAssignedUsers = await _context.UserRoles.AnyAsync(ur => ur.RoleId == id, cancellationToken);
        if (hasAssignedUsers)
        {
            return Result.Failure("Peran tidak dapat dihapus permanen karena masih ditugaskan kepada pengguna. Silakan nonaktifkan status peran ini.");
        }

        // Remove permissions
        var perms = await _context.RolePermissions.Where(rp => rp.RoleId == id).ToListAsync(cancellationToken);
        _context.RolePermissions.RemoveRange(perms);
        await _context.SaveChangesAsync(cancellationToken);

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            return Result.Failure(result.Errors.Select(e => e.Description));
        }

        return Result.Success();
    }

    public async Task<Result> ToggleRoleStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null)
        {
            return Result.Failure("Peran (role) tidak ditemukan.");
        }

        if (role.Name != null && SystemRoles.Contains(role.Name) && role.IsActive)
        {
            return Result.Failure($"Peran sistem bawaan '{role.Name}' tidak boleh dinonaktifkan.");
        }

        role.IsActive = !role.IsActive;
        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            return Result.Failure(result.Errors.Select(e => e.Description));
        }

        return Result.Success();
    }
}

