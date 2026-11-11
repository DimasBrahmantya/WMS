using Microsoft.EntityFrameworkCore;
using Quartz;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Notifications;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class StockAlertJob : IJob
{
    private readonly IWmsDbContext _context;
    private readonly INotificationService _notificationService;

    public StockAlertJob(IWmsDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        var balanceGroups = await _context.InventoryBalances
            .Include(b => b.Product)
            .Include(b => b.Warehouse)
            .Where(b => b.Product.IsActive)
            .GroupBy(b => new
            {
                b.WarehouseId,
                b.ProductId,
                WarehouseName = b.Warehouse.Name,
                ProductName = b.Product.Name,
                ProductSku = b.Product.Sku,
                MinLevel = b.Product.MinStock
            })
            .Select(g => new
            {
                g.Key.WarehouseId,
                g.Key.ProductId,
                g.Key.WarehouseName,
                g.Key.ProductName,
                g.Key.ProductSku,
                g.Key.MinLevel,
                TotalOnHand = g.Sum(x => x.OnHand)
            })
            .ToListAsync(cancellationToken);

        foreach (var item in balanceGroups)
        {
            var lowStockKey = $"LowStock:{item.WarehouseId}:{item.ProductId}";
            var outOfStockKey = $"OutOfStock:{item.WarehouseId}:{item.ProductId}";

            if (item.TotalOnHand <= 0)
            {
                await _notificationService.PublishNotificationAsync(new PublishNotificationRequest(
                    UserId: null,
                    TargetRole: "AdminGudang",
                    Category: NotificationCategory.StockAlert,
                    Title: "Stok Habis!",
                    Message: $"Barang {item.ProductName} ({item.ProductSku}) telah habis di gudang {item.WarehouseName}.",
                    LinkUrl: "/reports/stock",
                    DedupKey: outOfStockKey
                ), cancellationToken);

                await _notificationService.ResolveNotificationByDedupKeyAsync(lowStockKey, cancellationToken);
            }
            else if (item.TotalOnHand <= item.MinLevel)
            {
                await _notificationService.PublishNotificationAsync(new PublishNotificationRequest(
                    UserId: null,
                    TargetRole: "AdminGudang",
                    Category: NotificationCategory.StockAlert,
                    Title: "Peringatan Stok Menipis",
                    Message: $"Barang {item.ProductName} ({item.ProductSku}) tersisa {item.TotalOnHand} di gudang {item.WarehouseName} (Batas minimum: {item.MinLevel}).",
                    LinkUrl: "/reports/stock",
                    DedupKey: lowStockKey
                ), cancellationToken);

                await _notificationService.ResolveNotificationByDedupKeyAsync(outOfStockKey, cancellationToken);
            }
            else
            {
                // Kondisi pulih: stok di atas batas minimum
                await _notificationService.ResolveNotificationByDedupKeyAsync(lowStockKey, cancellationToken);
                await _notificationService.ResolveNotificationByDedupKeyAsync(outOfStockKey, cancellationToken);
            }
        }
    }
}
