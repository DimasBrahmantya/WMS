using Quartz;
using Wms.Application.Features.Audit;
using Wms.Application.Features.Notifications;
using Wms.Application.Features.Stock.Reconciliation;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class ReconciliationJob : IJob
{
    private readonly IStockReconciliationService _reconciliationService;
    private readonly INotificationService _notificationService;
    private readonly IAppAuditService _auditService;

    public ReconciliationJob(
        IStockReconciliationService reconciliationService,
        INotificationService notificationService,
        IAppAuditService auditService)
    {
        _reconciliationService = reconciliationService;
        _notificationService = notificationService;
        _auditService = auditService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        var report = await _reconciliationService.ReconcileAsync(null, cancellationToken);

        if (report.DiscrepancyCount > 0)
        {
            foreach (var d in report.Discrepancies)
            {
                var dedupKey = $"ReconDiff:{d.WarehouseId}:{d.ProductId}:{d.WarehouseLocationId}";

                await _notificationService.PublishNotificationAsync(new PublishNotificationRequest(
                    UserId: null,
                    TargetRole: "SuperAdmin",
                    Category: NotificationCategory.Reconciliation,
                    Title: "Peringatan Selisih Saldo vs Ledger",
                    Message: $"Ditemukan ketidaksesuaian saldo ({d.BalanceOnHand}) vs ledger ({d.LedgerOnHand}) pada {d.ProductName} ({d.ProductSku}) di {d.WarehouseName} / {d.WarehouseLocationCode}.",
                    LinkUrl: "/admin/audit-logs",
                    DedupKey: dedupKey
                ), cancellationToken);

                await _auditService.LogAsync(
                    "Reconciliation",
                    $"{d.WarehouseId}:{d.ProductId}",
                    "DiscrepancyDetected",
                    null,
                    null,
                    $"{{\"balanceOnHand\":{d.BalanceOnHand},\"ledgerOnHand\":{d.LedgerOnHand},\"difference\":{d.Difference}}}",
                    "Anomali saldo vs pergerakan ledger terdeteksi otomatis oleh background job",
                    cancellationToken);
            }
        }
    }
}
