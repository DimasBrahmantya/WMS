using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Entities.System;
using Wms.Domain.Entities.Transactions;

namespace Wms.Application.Common.Interfaces;

public interface IWmsDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<ApplicationRole> Roles { get; }
    DbSet<ApplicationUserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserWarehouse> UserWarehouses { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AppSetting> AppSettings { get; }

    // System / Sequence / Idempotency
    DbSet<DocumentSequence> DocumentSequences { get; }
    DbSet<IdempotencyKey> IdempotencyKeys { get; }

    // Master Data
    DbSet<Warehouse> Warehouses { get; }
    DbSet<WarehouseLocation> WarehouseLocations { get; }
    DbSet<Category> Categories { get; }
    DbSet<Unit> Units { get; }
    DbSet<Product> Products { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Customer> Customers { get; }

    // Persediaan & Ledger (Append-only)
    DbSet<InventoryBalance> InventoryBalances { get; }
    DbSet<InventoryValuation> InventoryValuations { get; }
    DbSet<StockMovement> StockMovements { get; }

    // Transaksi Stok (Fase 1)
    DbSet<GoodsReceipt> GoodsReceipts { get; }
    DbSet<GoodsReceiptLine> GoodsReceiptLines { get; }
    DbSet<StockOut> StockOuts { get; }
    DbSet<StockOutLine> StockOutLines { get; }

    DatabaseFacade Database { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
