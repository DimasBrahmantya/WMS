using Microsoft.EntityFrameworkCore;
using Quartz;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Notifications;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class OverduePurchaseOrderJob : IJob
{
    private readonly IWmsDbContext _context;
    private readonly INotificationService _notificationService;

    public OverduePurchaseOrderJob(IWmsDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var now = DateTime.UtcNow;

        var overduePOs = await _context.PurchaseOrders
            .Include(p => p.Supplier)
            .Where(p => (p.Status == PurchaseOrderStatus.Ordered || p.Status == PurchaseOrderStatus.PartiallyReceived)
                        && p.ExpectedDeliveryDate.HasValue
                        && p.ExpectedDeliveryDate.Value < now)
            .ToListAsync(cancellationToken);

        foreach (var po in overduePOs)
        {
            var dedupKey = $"OverduePo:{po.Id}";
            var supplierName = po.Supplier?.Name ?? "Pemasok";
            var dateStr = po.ExpectedDeliveryDate?.ToString("yyyy-MM-dd") ?? "-";

            await _notificationService.PublishNotificationAsync(new PublishNotificationRequest(
                UserId: po.CreatedByUserId.ToString(),
                TargetRole: "AdminGudang",
                Category: NotificationCategory.PurchaseOrder,
                Title: "Keterlambatan Pengiriman PO",
                Message: $"PO #{po.PoNumber} dari {supplierName} melewati estimasi pengiriman ({dateStr}).",
                LinkUrl: "/operations/purchase-orders",
                DedupKey: dedupKey
            ), cancellationToken);
        }

        // Resolving POs that were previously flagged but now completed or cancelled
        var resolvedPoIds = await _context.PurchaseOrders
            .Where(p => p.Status == PurchaseOrderStatus.Received || p.Status == PurchaseOrderStatus.Cancelled)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in resolvedPoIds)
        {
            await _notificationService.ResolveNotificationByDedupKeyAsync($"OverduePo:{id}", cancellationToken);
        }
    }
}
