using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Orders;
using Wms.Domain.Entities.System;

namespace Wms.Infrastructure.Persistence.Configurations;

public static class Tahap7Configurations
{
    public static void Configure(
        ModelBuilder builder,
        bool isUserRestrictedToWarehouses = false,
        IReadOnlyList<Guid>? userAllowedWarehouseIds = null)
    {
        builder.ApplyConfiguration(new BatchConfiguration());
        builder.ApplyConfiguration(new SalesOrderConfiguration());
        builder.ApplyConfiguration(new SalesOrderLineConfiguration());
        builder.ApplyConfiguration(new ApiKeyConfiguration());

        if (isUserRestrictedToWarehouses && userAllowedWarehouseIds != null)
        {
            builder.Entity<SalesOrder>()
                .HasQueryFilter(so => userAllowedWarehouseIds.Contains(so.WarehouseId));
        }
    }
}

public class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.ToTable("batches");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BatchNumber).HasMaxLength(100).IsRequired();
        builder.Property(b => b.BlockReason).HasMaxLength(500);

        builder.HasOne(b => b.Product)
            .WithMany()
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.ProductId, b.BatchNumber }).IsUnique();
        builder.HasIndex(b => b.ExpiryDate);
        builder.HasIndex(b => b.IsBlocked);
    }
}

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("sales_orders");
        builder.HasKey(so => so.Id);

        builder.Property(so => so.OrderNumber).HasMaxLength(50).IsRequired();
        builder.Property(so => so.Notes).HasMaxLength(1000);
        builder.Property(so => so.CancellationReason).HasMaxLength(500);
        builder.Property(so => so.TotalAmount).HasPrecision(18, 2);
        builder.Property(so => so.ConcurrencyToken).HasMaxLength(100).IsConcurrencyToken();

        builder.HasOne(so => so.Customer)
            .WithMany()
            .HasForeignKey(so => so.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(so => so.Warehouse)
            .WithMany()
            .HasForeignKey(so => so.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(so => so.CreatedByUser)
            .WithMany()
            .HasForeignKey(so => so.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(so => so.ConfirmedByUser)
            .WithMany()
            .HasForeignKey(so => so.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(so => so.DispatchedByUser)
            .WithMany()
            .HasForeignKey(so => so.DispatchedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(so => so.Lines)
            .WithOne(l => l.SalesOrder)
            .HasForeignKey(l => l.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(so => so.OrderNumber).IsUnique();
        builder.HasIndex(so => new { so.WarehouseId, so.Status });
        builder.HasIndex(so => so.OrderDate);
    }
}

public class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        builder.ToTable("sales_order_lines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.OrderedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.ReservedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.DispatchedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.Notes).HasMaxLength(500);

        builder.HasOne(l => l.Product)
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.SalesOrderId, l.ProductId });
    }
}

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Name).HasMaxLength(150).IsRequired();
        builder.Property(k => k.KeyPrefix).HasMaxLength(32).IsRequired();
        builder.Property(k => k.KeyHash).HasMaxLength(128).IsRequired();
        builder.Property(k => k.Scopes).HasMaxLength(500).IsRequired();

        builder.HasIndex(k => k.KeyPrefix);
        builder.HasIndex(k => k.KeyHash).IsUnique();
        builder.HasIndex(k => k.IsActive);
    }
}

