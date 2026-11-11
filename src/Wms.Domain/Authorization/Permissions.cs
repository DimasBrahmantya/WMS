namespace Wms.Domain.Authorization;

public static class Permissions
{
    // Pengguna dan Role
    public const string UsersView = "users.view";
    public const string UsersCreate = "users.create";
    public const string UsersEdit = "users.edit";
    public const string UsersDelete = "users.delete";
    public const string RolesView = "roles.view";
    public const string RolesEdit = "roles.edit";

    // Master Data
    public const string MasterWarehouseView = "master.warehouse.view";
    public const string MasterWarehouseManage = "master.warehouse.manage";
    public const string MasterProductView = "master.product.view";
    public const string MasterProductManage = "master.product.manage";
    public const string MasterCategoryManage = "master.category.manage";
    public const string MasterUnitManage = "master.unit.manage";
    public const string MasterSupplierManage = "master.supplier.manage";
    public const string MasterCustomerManage = "master.customer.manage";

    // Transaksi Stok (Fase 1)
    public const string StockInView = "stock.in.view";
    public const string StockInCreate = "stock.in.create";
    public const string StockInPost = "stock.in.post";
    public const string StockInReverse = "stock.in.reverse";

    public const string StockOutView = "stock.out.view";
    public const string StockOutCreate = "stock.out.create";
    public const string StockOutPost = "stock.out.post";
    public const string StockOutReverse = "stock.out.reverse";

    // Workflow Operasional (Fase 2)
    public const string PurchaseOrderView = "po.view";
    public const string PurchaseOrderCreate = "po.create";
    public const string PurchaseOrderApprove = "po.approve";

    public const string StockTransferView = "stock.transfer.view";
    public const string StockTransferCreate = "stock.transfer.create";
    public const string StockTransferApprove = "stock.transfer.approve";

    public const string StockOpnameView = "stock.opname.view";
    public const string StockOpnameManage = "stock.opname.manage";
    public const string StockOpnameApprove = "stock.opname.approve";

    public const string StockIncidentView = "stock.incident.view";
    public const string StockIncidentManage = "stock.incident.manage";
    public const string StockIncidentApprove = "stock.incident.approve";

    public const string StockReturnView = "stock.return.view";
    public const string StockReturnCreate = "stock.return.create";
    public const string StockReturnApprove = "stock.return.approve";

    // Laporan, Audit, Pengaturan
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";
    public const string AuditView = "audit.view";
    public const string SettingsManage = "settings.manage";

    // Tahap 7 — Fitur Lanjutan
    public const string BatchView = "batch.view";
    public const string BatchManage = "batch.manage";
    public const string SalesOrderView = "salesorder.view";
    public const string SalesOrderCreate = "salesorder.create";
    public const string SalesOrderApprove = "salesorder.approve";
    public const string SalesOrderPost = "salesorder.post";
    public const string ImportManage = "import.manage";
    public const string ApiKeysManage = "api.keys.manage";
    public const string SystemBackup = "system.backup";

    public static IReadOnlyList<string> All => new[]
    {
        UsersView, UsersCreate, UsersEdit, UsersDelete, RolesView, RolesEdit,
        MasterWarehouseView, MasterWarehouseManage, MasterProductView, MasterProductManage,
        MasterCategoryManage, MasterUnitManage, MasterSupplierManage, MasterCustomerManage,
        StockInView, StockInCreate, StockInPost, StockInReverse,
        StockOutView, StockOutCreate, StockOutPost, StockOutReverse,
        PurchaseOrderView, PurchaseOrderCreate, PurchaseOrderApprove,
        StockTransferView, StockTransferCreate, StockTransferApprove,
        StockOpnameView, StockOpnameManage, StockOpnameApprove,
        StockIncidentView, StockIncidentManage, StockIncidentApprove,
        StockReturnView, StockReturnCreate, StockReturnApprove,
        ReportsView, ReportsExport, AuditView, SettingsManage,
        BatchView, BatchManage, SalesOrderView, SalesOrderCreate, SalesOrderApprove, SalesOrderPost,
        ImportManage, ApiKeysManage, SystemBackup
    };
}
