namespace Wms.Application.Features.Stock.Reconciliation;

public interface IStockReconciliationService
{
    /// <summary>
    /// Membandingkan saldo saat ini (inventory_balances) dengan kalkulasi ledger (stock_movements).
    /// Juga mencocokkan total on_hand_qty di inventory_valuations dengan inventory_balances.
    /// Menghasilkan laporan selisih bila ada ketidakkonsistenan.
    /// </summary>
    Task<ReconciliationReport> ReconcileAsync(Guid? warehouseId = null, CancellationToken cancellationToken = default);
}

