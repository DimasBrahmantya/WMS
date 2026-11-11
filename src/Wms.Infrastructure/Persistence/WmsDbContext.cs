using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Common;
using Wms.Domain.Entities.Exports;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Entities.Notifications;
using Wms.Domain.Entities.System;
using Wms.Domain.Entities.Transactions;

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

    // System / Sequence / Idempotency
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    // Master Data
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseLocation> WarehouseLocations => Set<WarehouseLocation>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();

    // Persediaan & Ledger (Append-only)
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();
    public DbSet<InventoryValuation> InventoryValuations => Set<InventoryValuation>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    // Transaksi Stok (Fase 1)
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<StockOut> StockOuts => Set<StockOut>();
    public DbSet<StockOutLine> StockOutLines => Set<StockOutLine>();

    // Workflow Operasional (Fase 2)
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferLine> StockTransferLines => Set<StockTransferLine>();
    public DbSet<StockOpnameSession> StockOpnameSessions => Set<StockOpnameSession>();
    public DbSet<StockOpnameLine> StockOpnameLines => Set<StockOpnameLine>();
    public DbSet<StockReturn> StockReturns => Set<StockReturn>();
    public DbSet<StockReturnLine> StockReturnLines => Set<StockReturnLine>();
    public DbSet<StockIncident> StockIncidents => Set<StockIncident>();
    public DbSet<StockIncidentLine> StockIncidentLines => Set<StockIncidentLine>();

    // Notifikasi & Ekspor (Fase 2 / Tahap 6)
    public DbSet<AppNotification> AppNotifications => Set<AppNotification>();
    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return Task.FromResult<IDbContextTransaction>(new NullDbContextTransaction());
        }
        return Database.BeginTransactionAsync(cancellationToken);
    }

    private class NullDbContextTransaction : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public void Commit() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Rollback() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Enforce append-only ledger in ORM layer
        foreach (var entry in ChangeTracker.Entries<StockMovement>())
        {
            if (entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException("StockMovements bersifat append-only dan tidak boleh dihapus.");
            }

            if (entry.State == EntityState.Modified)
            {
                var modifiedProps = entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToList();
                var allowedMods = new[]
                {
                    nameof(StockMovement.ReversedByMovementId),
                    nameof(StockMovement.ReversedAtUtc),
                    nameof(StockMovement.Status)
                };

                if (modifiedProps.Any(p => !allowedMods.Contains(p)))
                {
                    throw new InvalidOperationException("Kolom nilai transaksi pada StockMovements tidak dapat diubah (append-only ledger).");
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

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
            entity.Property(p => p.MinStock).HasPrecision(18, 4).HasDefaultValue(0);
            entity.Property(p => p.MaxStock).HasPrecision(18, 4);
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

        // System / Sequences / Idempotency
        builder.Entity<DocumentSequence>(entity =>
        {
            entity.ToTable("document_sequences");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Prefix).HasMaxLength(10).IsRequired();
            entity.Property(s => s.Period).HasMaxLength(6).IsRequired();
            entity.HasIndex(s => new { s.Prefix, s.Period }).IsUnique();
            entity.Property(s => s.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        });

        builder.Entity<IdempotencyKey>(entity =>
        {
            entity.ToTable("idempotency_keys");
            entity.HasKey(k => k.Id);
            entity.Property(k => k.Operation).HasMaxLength(100).IsRequired();
            entity.Property(k => k.Key).HasMaxLength(100).IsRequired();
            entity.HasIndex(k => new { k.UserId, k.Operation, k.Key }).IsUnique();
            entity.Property(k => k.CreatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasOne(k => k.User)
                .WithMany()
                .HasForeignKey(k => k.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Persediaan: Balances & Valuations
        builder.Entity<InventoryBalance>(entity =>
        {
            entity.ToTable("inventory_balances", t =>
            {
                t.HasCheckConstraint("CK_inventory_balances_on_hand", "\"on_hand\" >= 0");
                t.HasCheckConstraint("CK_inventory_balances_reserved", "\"reserved\" >= 0 AND \"reserved\" <= \"on_hand\"");
            });

            entity.HasKey(b => b.Id);
            entity.Property(b => b.BatchNumber).HasMaxLength(100);
            entity.Property(b => b.Status).HasConversion<int>().IsRequired();
            entity.Property(b => b.OnHand).HasPrecision(18, 4);
            entity.Property(b => b.Reserved).HasPrecision(18, 4).HasDefaultValue(0);
            entity.Property(b => b.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(b => b.UpdatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(b => new { b.ProductId, b.WarehouseId, b.WarehouseLocationId, b.Status, b.BatchNumber }).IsUnique();

            entity.HasOne(b => b.Product)
                .WithMany()
                .HasForeignKey(b => b.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Warehouse)
                .WithMany()
                .HasForeignKey(b => b.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.WarehouseLocation)
                .WithMany()
                .HasForeignKey(b => b.WarehouseLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(b => !IsUserRestrictedToWarehouses || UserAllowedWarehouseIds.Contains(b.WarehouseId));
        });

        builder.Entity<InventoryValuation>(entity =>
        {
            entity.ToTable("inventory_valuations");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.OnHandQty).HasPrecision(18, 4);
            entity.Property(v => v.OnHandValue).HasPrecision(18, 4);
            entity.Property(v => v.AvgCost).HasPrecision(18, 4);
            entity.Property(v => v.LastCost).HasPrecision(18, 4);
            entity.Property(v => v.UpdatedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(v => new { v.ProductId, v.WarehouseId }).IsUnique();

            entity.HasOne(v => v.Product)
                .WithMany()
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.Warehouse)
                .WithMany()
                .HasForeignKey(v => v.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(v => !IsUserRestrictedToWarehouses || UserAllowedWarehouseIds.Contains(v.WarehouseId));
        });

        // Persediaan: Append-Only Stock Movements Ledger
        builder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements", t =>
            {
                t.HasCheckConstraint("CK_stock_movements_quantity", "\"quantity\" > 0");
            });

            entity.HasKey(m => m.Id);
            entity.Property(m => m.MovementNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(m => m.MovementNumber).IsUnique();

            entity.Property(m => m.MovementType).HasConversion<int>().IsRequired();
            entity.Property(m => m.StockStatus).HasConversion<int>().IsRequired();
            entity.Property(m => m.Direction).HasConversion<int>().IsRequired();
            entity.Property(m => m.Status).HasConversion<int>().IsRequired();
            entity.Property(m => m.SourceDocumentType).HasConversion<int>().IsRequired();

            entity.Property(m => m.BatchNumber).HasMaxLength(100);
            entity.Property(m => m.Quantity).HasPrecision(18, 4);
            entity.Property(m => m.BalanceBefore).HasPrecision(18, 4);
            entity.Property(m => m.BalanceAfter).HasPrecision(18, 4);
            entity.Property(m => m.UnitCost).HasPrecision(18, 4);
            entity.Property(m => m.TotalValue).HasPrecision(18, 4);

            entity.Property(m => m.SourceDocumentNumber).HasMaxLength(50).IsRequired();
            entity.Property(m => m.Reason).HasMaxLength(500);
            entity.Property(m => m.TimestampUtc).HasColumnType("timestamp with time zone");
            entity.Property(m => m.ReversedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(m => new { m.ProductId, m.WarehouseId, m.TimestampUtc });
            entity.HasIndex(m => new { m.SourceDocumentType, m.SourceDocumentId });
            entity.HasIndex(m => m.Status);

            entity.HasOne(m => m.Product)
                .WithMany()
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Warehouse)
                .WithMany()
                .HasForeignKey(m => m.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.WarehouseLocation)
                .WithMany()
                .HasForeignKey(m => m.WarehouseLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.ReversedByMovement)
                .WithMany()
                .HasForeignKey(m => m.ReversedByMovementId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(m => !IsUserRestrictedToWarehouses || UserAllowedWarehouseIds.Contains(m.WarehouseId));
        });

        // Transaksi Stok: Goods Receipts (Penerimaan / Masuk)
        builder.Entity<GoodsReceipt>(entity =>
        {
            entity.ToTable("goods_receipts");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.ReceiptNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(r => r.ReceiptNumber).IsUnique();

            entity.Property(r => r.DocumentDate).HasColumnType("timestamp with time zone");
            entity.Property(r => r.PurchaseOrderNumber).HasMaxLength(100);
            entity.Property(r => r.Notes).HasMaxLength(1000);
            entity.Property(r => r.Status).HasConversion<int>().IsRequired();
            entity.Property(r => r.ReversalReason).HasMaxLength(500);
            entity.Property(r => r.ConcurrencyToken).HasMaxLength(40).IsConcurrencyToken();

            entity.Property(r => r.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(r => r.UpdatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(r => r.PostedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(r => r.ReversedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(r => new { r.WarehouseId, r.DocumentDate, r.Status });

            entity.HasOne(r => r.Warehouse)
                .WithMany()
                .HasForeignKey(r => r.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Supplier)
                .WithMany()
                .HasForeignKey(r => r.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.CreatedByUser)
                .WithMany()
                .HasForeignKey(r => r.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.PostedByUser)
                .WithMany()
                .HasForeignKey(r => r.PostedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.ReversedByUser)
                .WithMany()
                .HasForeignKey(r => r.ReversedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(r => r.Lines)
                .WithOne(l => l.GoodsReceipt)
                .HasForeignKey(l => l.GoodsReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(r => !IsUserRestrictedToWarehouses || UserAllowedWarehouseIds.Contains(r.WarehouseId));
        });

        builder.Entity<GoodsReceiptLine>(entity =>
        {
            entity.ToTable("goods_receipt_lines", t =>
            {
                t.HasCheckConstraint("CK_goods_receipt_lines_quantity", "\"quantity\" > 0");
                t.HasCheckConstraint("CK_goods_receipt_lines_unit_cost", "\"unit_cost\" >= 0");
            });

            entity.HasKey(l => l.Id);
            entity.Property(l => l.Quantity).HasPrecision(18, 4);
            entity.Property(l => l.UnitCost).HasPrecision(18, 4);
            entity.Property(l => l.TotalCost).HasPrecision(18, 4);
            entity.Property(l => l.BatchNumber).HasMaxLength(100);
            entity.Property(l => l.ExpiryDate).HasColumnType("timestamp with time zone");
            entity.Property(l => l.Notes).HasMaxLength(500);

            entity.HasOne(l => l.Product)
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.WarehouseLocation)
                .WithMany()
                .HasForeignKey(l => l.WarehouseLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Transaksi Stok: Stock Outs (Pengeluaran / Keluar)
        builder.Entity<StockOut>(entity =>
        {
            entity.ToTable("stock_outs");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.IssueNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(s => s.IssueNumber).IsUnique();

            entity.Property(s => s.DocumentDate).HasColumnType("timestamp with time zone");
            entity.Property(s => s.Recipient).HasMaxLength(200);
            entity.Property(s => s.IssueType).HasConversion<int>().IsRequired();
            entity.Property(s => s.Notes).HasMaxLength(1000);
            entity.Property(s => s.Status).HasConversion<int>().IsRequired();
            entity.Property(s => s.ReversalReason).HasMaxLength(500);
            entity.Property(s => s.ConcurrencyToken).HasMaxLength(40).IsConcurrencyToken();

            entity.Property(s => s.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(s => s.UpdatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(s => s.PostedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(s => s.ReversedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(s => new { s.WarehouseId, s.DocumentDate, s.Status });

            entity.HasOne(s => s.Warehouse)
                .WithMany()
                .HasForeignKey(s => s.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Customer)
                .WithMany()
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.CreatedByUser)
                .WithMany()
                .HasForeignKey(s => s.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.PostedByUser)
                .WithMany()
                .HasForeignKey(s => s.PostedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.ReversedByUser)
                .WithMany()
                .HasForeignKey(s => s.ReversedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(s => s.Lines)
                .WithOne(l => l.StockOut)
                .HasForeignKey(l => l.StockOutId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(s => !IsUserRestrictedToWarehouses || UserAllowedWarehouseIds.Contains(s.WarehouseId));
        });

        builder.Entity<StockOutLine>(entity =>
        {
            entity.ToTable("stock_out_lines", t =>
            {
                t.HasCheckConstraint("CK_stock_out_lines_quantity", "\"quantity\" > 0");
            });

            entity.HasKey(l => l.Id);
            entity.Property(l => l.Quantity).HasPrecision(18, 4);
            entity.Property(l => l.UnitCost).HasPrecision(18, 4);
            entity.Property(l => l.TotalCost).HasPrecision(18, 4);
            entity.Property(l => l.BatchNumber).HasMaxLength(100);
            entity.Property(l => l.Notes).HasMaxLength(500);

            entity.HasOne(l => l.Product)
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.WarehouseLocation)
                .WithMany()
                .HasForeignKey(l => l.WarehouseLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        Configurations.OperationalWorkflowConfigurations.Configure(builder, IsUserRestrictedToWarehouses, UserAllowedWarehouseIds);
        Configurations.NotificationAndExportConfigurations.Configure(builder);
    }
}
