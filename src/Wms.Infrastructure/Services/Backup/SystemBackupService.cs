using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Backup;
using Wms.Domain.Common;

namespace Wms.Infrastructure.Services.Backup;

public class SystemBackupService : ISystemBackupService
{
    private readonly IWmsDbContext _context;
    private readonly IClock _clock;

    public SystemBackupService(IWmsDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<BackupResultDto> CreateSnapshotBackupAsync(string exportedBy, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var products = await _context.Products.AsNoTracking().ToListAsync(cancellationToken);
        var categories = await _context.Categories.AsNoTracking().ToListAsync(cancellationToken);
        var warehouses = await _context.Warehouses.AsNoTracking().ToListAsync(cancellationToken);
        var locations = await _context.WarehouseLocations.AsNoTracking().ToListAsync(cancellationToken);
        var batches = await _context.Batches.AsNoTracking().ToListAsync(cancellationToken);
        var balances = await _context.InventoryBalances.AsNoTracking().ToListAsync(cancellationToken);
        var movements = await _context.StockMovements.AsNoTracking().ToListAsync(cancellationToken);
        var salesOrders = await _context.SalesOrders.Include(s => s.Lines).AsNoTracking().ToListAsync(cancellationToken);
        var suppliers = await _context.Suppliers.AsNoTracking().ToListAsync(cancellationToken);
        var customers = await _context.Customers.AsNoTracking().ToListAsync(cancellationToken);

        int totalRecords = products.Count + categories.Count + warehouses.Count + locations.Count +
                           batches.Count + balances.Count + movements.Count + salesOrders.Count +
                           suppliers.Count + customers.Count;

        var snapshot = new
        {
            Metadata = new BackupMetadataDto
            {
                System = "WmsModularMonolith",
                Version = "1.0.0-Tahap7",
                ExportedAtUtc = now,
                ExportedBy = exportedBy,
                TotalRecords = totalRecords,
                Sha256Checksum = string.Empty // Will be set after computing content or embedded
            },
            Data = new
            {
                Products = products,
                Categories = categories,
                Warehouses = warehouses,
                Locations = locations,
                Batches = batches,
                InventoryBalances = balances,
                StockMovements = movements,
                SalesOrders = salesOrders,
                Suppliers = suppliers,
                Customers = customers
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, options);
        var hashBytes = SHA256.HashData(jsonBytes);
        var sha256Hex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var fileName = $"wms_backup_{now:yyyyMMdd_HHmmss}.json";

        return new BackupResultDto(
            FileName: fileName,
            Content: jsonBytes,
            ContentType: "application/json",
            ChecksumSha256: sha256Hex,
            RecordCount: totalRecords,
            ExportedAtUtc: now
        );
    }
}
