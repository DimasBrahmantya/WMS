using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Wms.Domain.Entities.Exports;
using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Entities.Notifications;
using Wms.Domain.Entities.Orders;
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

    // Workflow Operasional (Fase 2)
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<StockTransfer> StockTransfers { get; }
    DbSet<StockTransferLine> StockTransferLines { get; }
    DbSet<StockOpnameSession> StockOpnameSessions { get; }
    DbSet<StockOpnameLine> StockOpnameLines { get; }
    DbSet<StockReturn> StockReturns { get; }
    DbSet<StockReturnLine> StockReturnLines { get; }
    DbSet<StockIncident> StockIncidents { get; }
    DbSet<StockIncidentLine> StockIncidentLines { get; }

    // Notifikasi & Ekspor (Fase 2 / Tahap 6)
    DbSet<AppNotification> AppNotifications { get; }
    DbSet<ExportJob> ExportJobs { get; }

    // Tahap 7 — Fitur Lanjutan
    DbSet<Batch> Batches { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderLine> SalesOrderLines { get; }
    DbSet<ApiKey> ApiKeys { get; }

    DatabaseFacade Database { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
