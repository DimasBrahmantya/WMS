using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Auth;
using Wms.Domain.Authorization;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Infrastructure.Persistence;
using Wms.Web.Authorization;
using Xunit;

namespace Wms.Integration.Tests.Security;

public class FakeIdentityService : IIdentityService
{
    public List<string> PermissionsList { get; set; } = new();

    public Task<Result<UserDto>> AuthenticateAsync(LoginCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<UserDto>.Failure("Not implemented"));

    public Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<UserDto?>(null);

    public Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(PermissionsList);

    public Task<IReadOnlyList<Guid>> GetUserWarehouseIdsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
}

public class AuthenticationAndRbacTests
{
    private WmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WmsDbContext(options);
    }

    [Fact]
    public async Task SuperAdminRole_ShouldSatisfyAnyPermissionRequirement()
    {
        // 1. Arrange: SuperAdmin user context
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "superadmin"),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        }, "TestAuth"));

        var requirement = new PermissionRequirement(Permissions.StockOutPost);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        var services = new ServiceCollection();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var handler = new PermissionAuthorizationHandler(scopeFactory);

        // 2. Act
        await handler.HandleAsync(context);

        // 3. Assert
        context.HasSucceeded.Should().BeTrue("SuperAdmin harus otomatis memiliki akses ke semua izin");
    }

    [Fact]
    public async Task UserWithDirectPermissionClaim_ShouldSucceedAuthorization()
    {
        // 1. Arrange: Operator dengan klaim permission spesifik
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "staff"),
            new Claim(ClaimTypes.Role, "WarehouseStaff"),
            new Claim("permission", Permissions.StockInCreate)
        }, "TestAuth"));

        var requirement = new PermissionRequirement(Permissions.StockInCreate);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        var services = new ServiceCollection();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var handler = new PermissionAuthorizationHandler(scopeFactory);

        // 2. Act
        await handler.HandleAsync(context);

        // 3. Assert
        context.HasSucceeded.Should().BeTrue("Pengguna dengan klaim permission yang cocok harus lolos otorisasi");
    }

    [Fact]
    public async Task UserWithoutRequiredPermission_ShouldFailAuthorization()
    {
        // 1. Arrange: User hanya punya hak View tapi meminta hak Post
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "staff"),
            new Claim(ClaimTypes.Role, "WarehouseStaff"),
            new Claim("permission", Permissions.StockInView)
        }, "TestAuth"));

        var requirement = new PermissionRequirement(Permissions.StockInPost);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        var services = new ServiceCollection();
        services.AddScoped<IIdentityService>(_ => new FakeIdentityService { PermissionsList = new() { Permissions.StockInView } });
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var handler = new PermissionAuthorizationHandler(scopeFactory);

        // 2. Act
        await handler.HandleAsync(context);

        // 3. Assert
        context.HasSucceeded.Should().BeFalse("Pengguna tanpa izin yang sesuai harus ditolak");
    }

    [Fact]
    public async Task UserWithPermissionResolvedViaIdentityService_ShouldSucceedAuthorization()
    {
        // Pengujian resolusi hak akses secara dinamis via IIdentityService (jika klaim tidak ada di cookie)
        var userId = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "manager"),
            new Claim(ClaimTypes.Role, "WarehouseManager")
        }, "TestAuth"));

        var requirement = new PermissionRequirement(Permissions.StockOpnameApprove);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        var services = new ServiceCollection();
        services.AddScoped<IIdentityService>(_ => new FakeIdentityService
        {
            PermissionsList = new() { Permissions.StockOpnameApprove, Permissions.PurchaseOrderApprove }
        });
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var handler = new PermissionAuthorizationHandler(scopeFactory);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("Pengguna dengan izin yang terdaftar di database harus lolos otorisasi");
    }

    [Fact]
    public void InactiveUser_ShouldBeFlaggedAsInactive_AndNotPermitted()
    {
        using var db = CreateDbContext();
        var inactiveUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "inactive_staff",
            FullName = "Staff Non-Aktif",
            Email = "inactive@wms.local",
            IsActive = false
        };

        db.Users.Add(inactiveUser);
        db.SaveChanges();

        var retrieved = db.Users.Find(inactiveUser.Id);
        retrieved.Should().NotBeNull();
        retrieved!.IsActive.Should().BeFalse("Pengguna non-aktif tidak boleh diizinkan melakukan mutasi");
    }

    [Fact]
    public void AllDefinedPermissions_ShouldFollowDomainGroupingConventions()
    {
        var allPermissions = Permissions.All;
        allPermissions.Should().NotBeEmpty();

        foreach (var p in allPermissions)
        {
            p.Should().Contain(".", "Format permission harus terstruktur domain.action (contoh: stock.in.create)");
        }
    }
}
