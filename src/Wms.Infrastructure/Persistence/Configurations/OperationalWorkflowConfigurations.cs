using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities.Transactions;

namespace Wms.Infrastructure.Persistence.Configurations;

public static class OperationalWorkflowConfigurations
{
    public static void Configure(ModelBuilder builder, bool isUserRestricted, IReadOnlyList<Guid> allowedWarehouseIds)
    {
        // 1. Purchase Orders
        builder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("purchase_orders");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.PoNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(p => p.PoNumber).IsUnique();

            entity.Property(p => p.TotalAmount).HasPrecision(18, 4);
            entity.Property(p => p.Notes).HasMaxLength(1000);
            entity.Property(p => p.RejectionReason).HasMaxLength(500);
            entity.Property(p => p.ConcurrencyToken).HasMaxLength(100).IsRequired();

            entity.Property(p => p.OrderDate).HasColumnType("timestamp with time zone");
            entity.Property(p => p.ExpectedDeliveryDate).HasColumnType("timestamp with time zone");
            entity.Property(p => p.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(p => p.UpdatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(p => p.ApprovedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(p => new { p.WarehouseId, p.OrderDate, p.Status });

            entity.HasOne(p => p.Warehouse)
                .WithMany()
                .HasForeignKey(p => p.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Supplier)
                .WithMany()
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.CreatedByUser)
                .WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.ApprovedByUser)
                .WithMany()
                .HasForeignKey(p => p.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(p => p.Lines)
                .WithOne(l => l.PurchaseOrder)
                .HasForeignKey(l => l.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(p => !isUserRestricted || allowedWarehouseIds.Contains(p.WarehouseId));
        });

        builder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.ToTable("purchase_order_lines");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.OrderedQuantity).HasPrecision(18, 4);
            entity.Property(l => l.ReceivedQuantity).HasPrecision(18, 4);
            entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
            entity.Property(l => l.Notes).HasMaxLength(500);

            entity.HasOne(l => l.Product)
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 2. Stock Transfers
        builder.Entity<StockTransfer>(entity =>
        {
            entity.ToTable("stock_transfers");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TransferNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(t => t.TransferNumber).IsUnique();

            entity.Property(t => t.Reason).HasMaxLength(500);
            entity.Property(t => t.Notes).HasMaxLength(1000);
            entity.Property(t => t.RejectionReason).HasMaxLength(500);
            entity.Property(t => t.ConcurrencyToken).HasMaxLength(100).IsRequired();

            entity.Property(t => t.TransferDate).HasColumnType("timestamp with time zone");
            entity.Property(t => t.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(t => t.UpdatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(t => t.ApprovedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(t => t.DispatchedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(t => t.ReceivedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasIndex(t => new { t.OriginWarehouseId, t.DestinationWarehouseId, t.Status });

            entity.HasOne(t => t.OriginWarehouse)
                .WithMany()
                .HasForeignKey(t => t.OriginWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.DestinationWarehouse)
                .WithMany()
                .HasForeignKey(t => t.DestinationWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ApprovedByUser)
                .WithMany()
                .HasForeignKey(t => t.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.DispatchedByUser)
                .WithMany()
                .HasForeignKey(t => t.DispatchedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ReceivedByUser)
                .WithMany()
                .HasForeignKey(t => t.ReceivedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(t => t.Lines)
                .WithOne(l => l.StockTransfer)
                .HasForeignKey(l => l.StockTransferId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(t => !isUserRestricted || allowedWarehouseIds.Contains(t.OriginWarehouseId) || allowedWarehouseIds.Contains(t.DestinationWarehouseId));
        });

        builder.Entity<StockTransferLine>(entity =>
        {
            entity.ToTable("stock_transfer_lines");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.QuantityRequested).HasPrecision(18, 4);
            entity.Property(l => l.QuantityDispatched).HasPrecision(18, 4);
            entity.Property(l => l.QuantityReceived).HasPrecision(18, 4);
            entity.Property(l => l.Notes).HasMaxLength(500);

            entity.HasOne(l => l.Product)
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.OriginLocation)
                .WithMany()
                .HasForeignKey(l => l.OriginLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.DestinationLocation)
                .WithMany()
                .HasForeignKey(l => l.DestinationLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 3. Stock Opname
        builder.Entity<StockOpnameSession>(entity =>
        {
            entity.ToTable("stock_opname_sessions");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.SessionNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(o => o.SessionNumber).IsUnique();

            entity.Property(o => o.Notes).HasMaxLength(1000);
            entity.Property(o => o.RejectionReason).HasMaxLength(500);
            entity.Property(o => o.TotalVarianceQuantity).HasPrecision(18, 4);
            entity.Property(o => o.TotalVarianceValue).HasPrecision(18, 4);
            entity.Property(o => o.ConcurrencyToken).HasMaxLength(100).IsRequired();

            entity.Property(o => o.CutoffDate).HasColumnType("timestamp with time zone");
            entity.Property(o => o.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(o => o.UpdatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(o => o.ApprovedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(o => o.PostedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasOne(o => o.Warehouse)
                .WithMany()
                .HasForeignKey(o => o.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Category)
                .WithMany()
                .HasForeignKey(o => o.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Location)
                .WithMany()
                .HasForeignKey(o => o.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.CreatedByUser)
                .WithMany()
                .HasForeignKey(o => o.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.ApprovedByUser)
                .WithMany()
                .HasForeignKey(o => o.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.PostedByUser)
                .WithMany()
                .HasForeignKey(o => o.PostedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(o => o.Lines)
                .WithOne(l => l.StockOpnameSession)
                .HasForeignKey(l => l.StockOpnameSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(o => !isUserRestricted || allowedWarehouseIds.Contains(o.WarehouseId));
        });

        builder.Entity<StockOpnameLine>(entity =>
        {
            entity.ToTable("stock_opname_lines");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.SystemSnapshotQuantity).HasPrecision(18, 4);
            entity.Property(l => l.SystemSnapshotAvgCost).HasPrecision(18, 4);
            entity.Property(l => l.CountedQuantity).HasPrecision(18, 4);
            entity.Property(l => l.RecountQuantity).HasPrecision(18, 4);
            entity.Property(l => l.Notes).HasMaxLength(500);

            entity.HasOne(l => l.Product)
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Location)
                .WithMany()
                .HasForeignKey(l => l.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. Stock Returns
        builder.Entity<StockReturn>(entity =>
        {
            entity.ToTable("stock_returns");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.ReturnNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(r => r.ReturnNumber).IsUnique();

            entity.Property(r => r.ReferenceDocumentNumber).HasMaxLength(100);
            entity.Property(r => r.Reason).HasMaxLength(500);
            entity.Property(r => r.Notes).HasMaxLength(1000);
            entity.Property(r => r.RejectionReason).HasMaxLength(500);
            entity.Property(r => r.ConcurrencyToken).HasMaxLength(100).IsRequired();

            entity.Property(r => r.ReturnDate).HasColumnType("timestamp with time zone");
            entity.Property(r => r.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(r => r.UpdatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(r => r.ApprovedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(r => r.PostedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasOne(r => r.Warehouse)
                .WithMany()
                .HasForeignKey(r => r.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Supplier)
                .WithMany()
                .HasForeignKey(r => r.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.CreatedByUser)
                .WithMany()
                .HasForeignKey(r => r.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.ApprovedByUser)
                .WithMany()
                .HasForeignKey(r => r.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.PostedByUser)
                .WithMany()
                .HasForeignKey(r => r.PostedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(r => r.Lines)
                .WithOne(l => l.StockReturn)
                .HasForeignKey(l => l.StockReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(r => !isUserRestricted || allowedWarehouseIds.Contains(r.WarehouseId));
        });

        builder.Entity<StockReturnLine>(entity =>
        {
            entity.ToTable("stock_return_lines");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Quantity).HasPrecision(18, 4);
            entity.Property(l => l.UnitPrice).HasPrecision(18, 4);
            entity.Property(l => l.Notes).HasMaxLength(500);

            entity.HasOne(l => l.Product)
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Location)
                .WithMany()
                .HasForeignKey(l => l.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 5. Stock Incidents
        builder.Entity<StockIncident>(entity =>
        {
            entity.ToTable("stock_incidents");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.IncidentNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(i => i.IncidentNumber).IsUnique();

            entity.Property(i => i.Quantity).HasPrecision(18, 4);
            entity.Property(i => i.EstimatedValue).HasPrecision(18, 4);
            entity.Property(i => i.Reason).HasMaxLength(500);
            entity.Property(i => i.InvestigationNotes).HasMaxLength(1000);
            entity.Property(i => i.ActionTaken).HasMaxLength(500);
            entity.Property(i => i.RejectionReason).HasMaxLength(500);
            entity.Property(i => i.ConcurrencyToken).HasMaxLength(100).IsRequired();

            entity.Property(i => i.IncidentDate).HasColumnType("timestamp with time zone");
            entity.Property(i => i.ReportedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(i => i.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(i => i.UpdatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(i => i.ApprovedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(i => i.ResolvedAtUtc).HasColumnType("timestamp with time zone");

            entity.HasOne(i => i.Warehouse)
                .WithMany()
                .HasForeignKey(i => i.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.Location)
                .WithMany()
                .HasForeignKey(i => i.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.ReportedByUser)
                .WithMany()
                .HasForeignKey(i => i.ReportedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.InvestigatedByUser)
                .WithMany()
                .HasForeignKey(i => i.InvestigatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.ApprovedByUser)
                .WithMany()
                .HasForeignKey(i => i.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.ResolvedByUser)
                .WithMany()
                .HasForeignKey(i => i.ResolvedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(i => i.Lines)
                .WithOne(l => l.StockIncident)
                .HasForeignKey(l => l.StockIncidentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(i => !isUserRestricted || allowedWarehouseIds.Contains(i.WarehouseId));
        });

        builder.Entity<StockIncidentLine>(entity =>
        {
            entity.ToTable("stock_incident_lines");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Quantity).HasPrecision(18, 4);
            entity.Property(l => l.EstimatedValue).HasPrecision(18, 4);
            entity.Property(l => l.Notes).HasMaxLength(500);

            entity.HasOne(l => l.Product)
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Location)
                .WithMany()
                .HasForeignKey(l => l.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
