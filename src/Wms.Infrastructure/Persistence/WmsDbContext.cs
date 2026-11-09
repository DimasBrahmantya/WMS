using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Common;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Entities.System;

namespace Wms.Infrastructure.Persistence;

public class WmsDbContext : IdentityDbContext<
    ApplicationUser,
    ApplicationRole,
    Guid,
    IdentityUserClaim<Guid>,
    ApplicationUserRole,
    IdentityUserLogin<Guid>,
    IdentityRoleClaim<Guid>,
    IdentityUserToken<Guid>>, IWmsDbContext
{
    private readonly IUserContext? _userContext;

    public bool IsUserRestrictedToWarehouses =>
        _userContext != null &&
        _userContext.IsAuthenticated &&
        !_userContext.Roles.Contains("SuperAdmin") &&
        _userContext.AllowedWarehouseIds.Count > 0;

    public IReadOnlyList<Guid> UserAllowedWarehouseIds =>
        _userContext?.AllowedWarehouseIds ?? Array.Empty<Guid>();

    public WmsDbContext(DbContextOptions<WmsDbContext> options, IUserContext? userContext = null)
        : base(options)
    {
        _userContext = userContext;
    }

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserWarehouse> UserWarehouses => Set<UserWarehouse>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    // Master Data
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseLocation> WarehouseLocations => Set<WarehouseLocation>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure table names to snake_case
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            entity.Property(u => u.IsActive).HasDefaultValue(true);
            entity.Property(u => u.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(u => u.UpdatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasMany(u => u.UserRoles)
                .WithOne(ur => ur.User)
                .HasForeignKey(ur => ur.UserId)
                .IsRequired();

            entity.HasMany(u => u.UserWarehouses)
                .WithOne(uw => uw.User)
                .HasForeignKey(uw => uw.UserId)
                .IsRequired();
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("roles");
            entity.Property(r => r.Description).HasMaxLength(500);
            entity.Property(r => r.IsActive).HasDefaultValue(true);
            entity.Property(r => r.CreatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasMany(r => r.UserRoles)
                .WithOne(ur => ur.Role)
                .HasForeignKey(ur => ur.RoleId)
                .IsRequired();

            entity.HasMany(r => r.RolePermissions)
                .WithOne(rp => rp.Role)
                .HasForeignKey(rp => rp.RoleId)
                .IsRequired();
        });

        builder.Entity<ApplicationUserRole>(entity =>
        {
            entity.ToTable("user_roles");
        });

        builder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(rp => rp.Id);
            entity.Property(rp => rp.Permission).HasMaxLength(100).IsRequired();
            entity.HasIndex(rp => new { rp.RoleId, rp.Permission }).IsUnique();
        });

        builder.Entity<UserWarehouse>(entity =>
        {
            entity.ToTable("user_warehouses");
            entity.HasKey(uw => uw.Id);
            entity.HasIndex(uw => new { uw.UserId, uw.WarehouseId }).IsUnique();

            entity.HasOne(uw => uw.Warehouse)
                .WithMany()
                .HasForeignKey(uw => uw.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Action).HasMaxLength(50).IsRequired();
            entity.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(a => a.EntityId).HasMaxLength(100);
            entity.Property(a => a.TimestampUtc).HasColumnType("timestamp with time zone");
            entity.Property(a => a.CorrelationId).HasMaxLength(100);
            entity.Property(a => a.IpAddress).HasMaxLength(50);
            entity.HasIndex(a => a.TimestampUtc);
            entity.HasIndex(a => a.EntityType);
            entity.HasIndex(a => a.UserId);
        });

        builder.Entity<AppSetting>(entity =>
        {
            entity.ToTable("app_settings");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Key).HasMaxLength(100).IsRequired();
            entity.HasIndex(s => s.Key).IsUnique();
            entity.Property(s => s.Value).IsRequired();
            entity.Property(s => s.Description).HasMaxLength(500);
            entity.Property(s => s.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(s => s.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        });

        // Other Identity Tables
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        // Master Data Mapping
        builder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("warehouses");
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(w => w.Code).IsUnique();
            entity.Property(w => w.Name).HasMaxLength(200).IsRequired();
            entity.Property(w => w.Address).HasMaxLength(500);
            entity.Property(w => w.ContactPerson).HasMaxLength(100);
            entity.Property(w => w.IsActive).HasDefaultValue(true);
            entity.Property(w => w.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(w => w.UpdatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasMany(w => w.Locations)
                .WithOne(l => l.Warehouse)
                .HasForeignKey(l => l.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(w => !IsUserRestrictedToWarehouses || UserAllowedWarehouseIds.Contains(w.Id));
        });

        builder.Entity<WarehouseLocation>(entity =>
        {
            entity.ToTable("warehouse_locations");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(l => new { l.WarehouseId, l.Code }).IsUnique();
            entity.Property(l => l.Name).HasMaxLength(100).IsRequired();
            entity.Property(l => l.Zone).HasMaxLength(50);
            entity.Property(l => l.Aisle).HasMaxLength(50);
            entity.Property(l => l.Rack).HasMaxLength(50);
            entity.Property(l => l.Bin).HasMaxLength(50);
            entity.Property(l => l.IsActive).HasDefaultValue(true);
            entity.Property(l => l.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(l => l.UpdatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasQueryFilter(l => !IsUserRestrictedToWarehouses || UserAllowedWarehouseIds.Contains(l.WarehouseId));
        });

        builder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(c => c.Name).IsUnique();
            entity.Property(c => c.Description).HasMaxLength(500);
            entity.Property(c => c.IsActive).HasDefaultValue(true);
            entity.Property(c => c.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(c => c.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        });

        builder.Entity<Unit>(entity =>
        {
            entity.ToTable("units");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(u => u.Name).IsUnique();
            entity.Property(u => u.Abbreviation).HasMaxLength(20).IsRequired();
            entity.HasIndex(u => u.Abbreviation).IsUnique();
            entity.Property(u => u.IsActive).HasDefaultValue(true);
            entity.Property(u => u.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(u => u.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        });

        builder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Sku).HasMaxLength(100).IsRequired();
            entity.HasIndex(p => p.Sku).IsUnique();
            entity.Property(p => p.Barcode).HasMaxLength(100);
            entity.HasIndex(p => p.Barcode).IsUnique().HasFilter("\"barcode\" IS NOT NULL");
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(1000);
            entity.Property(p => p.Brand).HasMaxLength(100);
            entity.Property(p => p.MinStock).HasColumnType("numeric(18,4)").HasDefaultValue(0);
            entity.Property(p => p.MaxStock).HasColumnType("numeric(18,4)");
            entity.Property(p => p.TrackingMethod).HasConversion<int>().IsRequired();
            entity.Property(p => p.IsActive).HasDefaultValue(true);
            entity.Property(p => p.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(p => p.UpdatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Unit)
                .WithMany(u => u.Products)
                .HasForeignKey(p => p.UnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(s => s.Code).IsUnique();
            entity.Property(s => s.Name).HasMaxLength(200).IsRequired();
            entity.Property(s => s.ContactPerson).HasMaxLength(100);
            entity.Property(s => s.Email).HasMaxLength(100);
            entity.Property(s => s.Phone).HasMaxLength(50);
            entity.Property(s => s.Address).HasMaxLength(500);
            entity.Property(s => s.TaxNumber).HasMaxLength(50);
            entity.Property(s => s.Notes).HasMaxLength(1000);
            entity.Property(s => s.IsActive).HasDefaultValue(true);
            entity.Property(s => s.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(s => s.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        });

        builder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(c => c.Code).IsUnique();
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.ContactPerson).HasMaxLength(100);
            entity.Property(c => c.Email).HasMaxLength(100);
            entity.Property(c => c.Phone).HasMaxLength(50);
            entity.Property(c => c.Address).HasMaxLength(500);
            entity.Property(c => c.TaxNumber).HasMaxLength(50);
            entity.Property(c => c.Notes).HasMaxLength(1000);
            entity.Property(c => c.IsActive).HasDefaultValue(true);
            entity.Property(c => c.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(c => c.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        });
    }
}
