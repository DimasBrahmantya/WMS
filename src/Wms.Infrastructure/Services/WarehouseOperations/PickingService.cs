using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Batches;
using Wms.Application.Features.WarehouseOperations;

namespace Wms.Infrastructure.Services.WarehouseOperations;

public class PickingService : IPickingService
{
    private readonly IWmsDbContext _context;
    private readonly IFefoService _fefoService;

    public PickingService(IWmsDbContext context, IFefoService fefoService)
    {
        _context = context;
        _fefoService = fefoService;
    }

    public async Task<PickingListDto> GeneratePickingListForSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.Warehouse)
            .Include(so => so.Lines)
                .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(so => so.Id == salesOrderId, cancellationToken);

        if (order == null)
        {
            throw new InvalidOperationException($"Sales Order dengan ID '{salesOrderId}' tidak ditemukan.");
        }

        var lines = new List<PickingListLineDto>();

        foreach (var line in order.Lines)
        {
            var allocations = await _fefoService.AllocateFefoAsync(
                line.ProductId,
                order.WarehouseId,
                line.OrderedQuantity,
                includeReserved: true,
                cancellationToken: cancellationToken);

            var locationIds = allocations.Select(a => a.WarehouseLocationId).Distinct().ToList();
            var locations = await _context.WarehouseLocations
                .Where(l => locationIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, cancellationToken);

            var itemAllocations = allocations.Select(a =>
            {
                locations.TryGetValue(a.WarehouseLocationId, out var loc);
                return new PickingItemAllocationDto
                {
                    WarehouseLocationId = a.WarehouseLocationId,
                    LocationCode = a.LocationCode,
                    Zone = loc?.Zone,
                    BatchNumber = a.BatchNumber,
                    ExpiryDate = a.ExpiryDate,
                    QuantityToPick = a.AllocatedQuantity
                };
            })
            .OrderBy(a => a.LocationCode) // Urutan jalur lorong rak gudang optimal
            .ToList();

            lines.Add(new PickingListLineDto
            {
                ProductId = line.ProductId,
                ProductSku = line.Product?.Sku ?? string.Empty,
                ProductName = line.Product?.Name ?? string.Empty,
                QuantityOrdered = line.OrderedQuantity,
                Allocations = itemAllocations
            });
        }

        return new PickingListDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerName = order.Customer?.Name ?? "Pelanggan Umum",
            WarehouseName = order.Warehouse?.Name ?? "Gudang Utama",
            OrderDate = order.OrderDate,
            Lines = lines
        };
    }
}
