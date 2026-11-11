using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Audit;
using Wms.Application.Features.Imports;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Domain.Common;
using Wms.Domain.Entities.Inventory;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Imports;

public class DataImportService : IDataImportService
{
    private readonly IWmsDbContext _context;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IAppAuditService _auditService;
    private readonly IClock _clock;

    public DataImportService(
        IWmsDbContext context,
        IDocumentSequenceService sequenceService,
        IAppAuditService auditService,
        IClock clock)
    {
        _context = context;
        _sequenceService = sequenceService;
        _auditService = auditService;
        _clock = clock;
    }

    public async Task<ImportValidationResultDto> ValidateProductImportAsync(
        Stream fileStream,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        var rawRows = ReadRows(fileStream, fileExtension);
        var result = new ImportValidationResultDto
        {
            ImportType = ImportType.Products,
            TotalRows = rawRows.Count
        };

        var existingSkus = await _context.Products.Select(p => p.Sku.ToLower()).ToListAsync(cancellationToken);
        var existingBarcodes = await _context.Products.Where(p => p.Barcode != null).Select(p => p.Barcode!.ToLower()).ToListAsync(cancellationToken);
        var categories = await _context.Categories.Where(c => c.IsActive).ToDictionaryAsync(c => c.Name.ToLower(), c => c.Id, cancellationToken);
        var allUnits = await _context.Units.Where(u => u.IsActive).ToListAsync(cancellationToken);
        var units = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in allUnits)
        {
            if (!string.IsNullOrWhiteSpace(u.Name)) units[u.Name] = u.Id;
            if (!string.IsNullOrWhiteSpace(u.Abbreviation)) units[u.Abbreviation] = u.Id;
        }

        var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var rowNum = 1;
        foreach (var dict in rawRows)
        {
            rowNum++;
            var sku = GetValue(dict, "Sku", "Kode Barang");
            var name = GetValue(dict, "Name", "Nama Barang");
            var barcode = GetValue(dict, "Barcode");
            var brand = GetValue(dict, "Brand", "Merek");
            var categoryName = GetValue(dict, "Category", "Kategori");
            var unitName = GetValue(dict, "Unit", "Satuan");
            var minStockStr = GetValue(dict, "MinStock", "Stok Minimum");
            var maxStockStr = GetValue(dict, "MaxStock", "Stok Maksimum");
            var trackingMethodStr = GetValue(dict, "TrackingMethod", "Metode Tracking");

            var rowErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(sku))
            {
                rowErrors.Add("SKU wajib diisi.");
            }
            else
            {
                if (existingSkus.Contains(sku.ToLower()))
                    rowErrors.Add($"SKU '{sku}' sudah terdaftar di sistem.");
                if (!seenSkus.Add(sku))
                    rowErrors.Add($"SKU '{sku}' duplikat di dalam file impor.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                rowErrors.Add("Nama barang wajib diisi.");
            }

            if (!string.IsNullOrWhiteSpace(barcode))
            {
                if (existingBarcodes.Contains(barcode.ToLower()))
                    rowErrors.Add($"Barcode '{barcode}' sudah terdaftar di sistem.");
                if (!seenBarcodes.Add(barcode))
                    rowErrors.Add($"Barcode '{barcode}' duplikat di dalam file impor.");
            }

            if (string.IsNullOrWhiteSpace(categoryName) || !categories.ContainsKey(categoryName.ToLower()))
            {
                rowErrors.Add($"Kategori '{categoryName}' tidak ditemukan atau tidak aktif.");
            }

            if (string.IsNullOrWhiteSpace(unitName) || !units.ContainsKey(unitName.ToLower()))
            {
                rowErrors.Add($"Satuan '{unitName}' tidak ditemukan atau tidak aktif.");
            }

            decimal minStock = 0;
            if (!string.IsNullOrWhiteSpace(minStockStr) && (!decimal.TryParse(minStockStr, NumberStyles.Any, CultureInfo.InvariantCulture, out minStock) || minStock < 0))
            {
                rowErrors.Add("Stok minimum harus berupa angka valid >= 0.");
            }

            decimal? maxStock = null;
            if (!string.IsNullOrWhiteSpace(maxStockStr))
            {
                if (decimal.TryParse(maxStockStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedMax))
                {
                    if (parsedMax < minStock) rowErrors.Add("Stok maksimum tidak boleh lebih kecil dari stok minimum.");
                    else maxStock = parsedMax;
                }
                else
                {
                    rowErrors.Add("Stok maksimum harus berupa angka valid.");
                }
            }

            var isValid = rowErrors.Count == 0;
            var errMessage = isValid ? null : string.Join("; ", rowErrors);

            if (!isValid)
            {
                result.Errors.Add(new ImportRowErrorDto
                {
                    RowNumber = rowNum,
                    Identifier = sku ?? $"Baris {rowNum}",
                    Field = "Product",
                    ErrorMessage = errMessage!
                });
            }

            result.ProductRows.Add(new ProductImportRowDto
            {
                RowNumber = rowNum,
                Sku = sku ?? string.Empty,
                Name = name ?? string.Empty,
                Barcode = barcode,
                Brand = brand,
                CategoryName = categoryName ?? string.Empty,
                UnitName = unitName ?? string.Empty,
                MinStock = minStock,
                MaxStock = maxStock,
                TrackingMethod = string.IsNullOrWhiteSpace(trackingMethodStr) ? "None" : trackingMethodStr,
                IsValid = isValid,
                ErrorMessage = errMessage
            });
        }

        result.ErrorRowsCount = result.Errors.Count;
        result.ValidRowsCount = result.ProductRows.Count - result.ErrorRowsCount;
        return result;
    }

    public async Task<ImportValidationResultDto> ValidateInitialStockImportAsync(
        Stream fileStream,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        var rawRows = ReadRows(fileStream, fileExtension);
        var result = new ImportValidationResultDto
        {
            ImportType = ImportType.InitialStock,
            TotalRows = rawRows.Count
        };

        var warehouses = await _context.Warehouses.Where(w => w.IsActive).ToDictionaryAsync(w => w.Code.ToLower(), w => w.Id, cancellationToken);
        var locations = await _context.WarehouseLocations.Where(l => l.IsActive).ToListAsync(cancellationToken);
        var products = await _context.Products.Where(p => p.IsActive).ToDictionaryAsync(p => p.Sku.ToLower(), p => p.Id, cancellationToken);

        var rowNum = 1;
        foreach (var dict in rawRows)
        {
            rowNum++;
            var whCode = GetValue(dict, "WarehouseCode", "Kode Gudang");
            var sku = GetValue(dict, "Sku", "Kode Barang");
            var locCode = GetValue(dict, "LocationCode", "Kode Lokasi");
            var batchNumber = GetValue(dict, "BatchNumber", "Nomor Batch");
            var expiryDateStr = GetValue(dict, "ExpiryDate", "Tanggal Kedaluwarsa");
            var qtyStr = GetValue(dict, "Quantity", "Jumlah", "Kuantitas");
            var costStr = GetValue(dict, "UnitCost", "Harga Satuan", "Biaya");

            var rowErrors = new List<string>();

            Guid warehouseId = Guid.Empty;
            if (string.IsNullOrWhiteSpace(whCode) || !warehouses.TryGetValue(whCode.ToLower(), out warehouseId))
            {
                rowErrors.Add($"Kode gudang '{whCode}' tidak ditemukan atau tidak aktif.");
            }

            if (string.IsNullOrWhiteSpace(sku) || !products.ContainsKey(sku.ToLower()))
            {
                rowErrors.Add($"SKU '{sku}' tidak ditemukan atau tidak aktif.");
            }

            if (string.IsNullOrWhiteSpace(locCode))
            {
                rowErrors.Add("Kode lokasi rak wajib diisi.");
            }
            else if (warehouseId != Guid.Empty)
            {
                var locExists = locations.Any(l => l.WarehouseId == warehouseId && string.Equals(l.Code, locCode, StringComparison.OrdinalIgnoreCase));
                if (!locExists)
                    rowErrors.Add($"Lokasi '{locCode}' tidak ditemukan di gudang '{whCode}'.");
            }

            decimal qty = 0;
            if (string.IsNullOrWhiteSpace(qtyStr) || !decimal.TryParse(qtyStr, NumberStyles.Any, CultureInfo.InvariantCulture, out qty) || qty <= 0)
            {
                rowErrors.Add("Kuantitas harus berupa angka valid > 0.");
            }

            decimal cost = 0;
            if (!string.IsNullOrWhiteSpace(costStr) && (!decimal.TryParse(costStr, NumberStyles.Any, CultureInfo.InvariantCulture, out cost) || cost < 0))
            {
                rowErrors.Add("Harga satuan harus berupa angka valid >= 0.");
            }

            DateOnly? expiry = null;
            if (!string.IsNullOrWhiteSpace(expiryDateStr))
            {
                if (DateOnly.TryParse(expiryDateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedExpiry))
                {
                    expiry = parsedExpiry;
                }
                else
                {
                    rowErrors.Add("Format tanggal kedaluwarsa tidak valid (gunakan format YYYY-MM-DD).");
                }
            }

            var isValid = rowErrors.Count == 0;
            var errMessage = isValid ? null : string.Join("; ", rowErrors);

            if (!isValid)
            {
                result.Errors.Add(new ImportRowErrorDto
                {
                    RowNumber = rowNum,
                    Identifier = $"{whCode}-{sku}",
                    Field = "InitialStock",
                    ErrorMessage = errMessage!
                });
            }

            result.StockRows.Add(new InitialStockImportRowDto
            {
                RowNumber = rowNum,
                WarehouseCode = whCode ?? string.Empty,
                Sku = sku ?? string.Empty,
                LocationCode = locCode ?? string.Empty,
                BatchNumber = batchNumber,
                ExpiryDate = expiry,
                Quantity = qty,
                UnitCost = cost,
                IsValid = isValid,
                ErrorMessage = errMessage
            });
        }

        result.ErrorRowsCount = result.Errors.Count;
        result.ValidRowsCount = result.StockRows.Count - result.ErrorRowsCount;
        return result;
    }

    public async Task<Result<ImportExecutionResultDto>> CommitImportAsync(
        CommitImportCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = _clock.UtcNow;

            if (command.ImportType == ImportType.Products)
            {
                var validRows = command.ProductRows.Where(r => r.IsValid).ToList();
                if (validRows.Count == 0)
                    return Result<ImportExecutionResultDto>.Failure("Tidak ada baris produk valid yang dapat diimpor.");

                var categories = await _context.Categories.ToDictionaryAsync(c => c.Name.ToLower(), c => c.Id, cancellationToken);
                var allUnits = await _context.Units.ToListAsync(cancellationToken);
                var units = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
                foreach (var u in allUnits)
                {
                    if (!string.IsNullOrWhiteSpace(u.Name)) units[u.Name] = u.Id;
                    if (!string.IsNullOrWhiteSpace(u.Abbreviation)) units[u.Abbreviation] = u.Id;
                }

                foreach (var row in validRows)
                {
                    var catId = categories[row.CategoryName.ToLower()];
                    var unitId = units[row.UnitName];

                    var tracking = TrackingMethod.None;
                    if (Enum.TryParse<TrackingMethod>(row.TrackingMethod, true, out var parsedTracking))
                        tracking = parsedTracking;

                    var product = new Product
                    {
                        Sku = row.Sku.Trim(),
                        Name = row.Name.Trim(),
                        Barcode = string.IsNullOrWhiteSpace(row.Barcode) ? null : row.Barcode.Trim(),
                        Brand = string.IsNullOrWhiteSpace(row.Brand) ? null : row.Brand.Trim(),
                        CategoryId = catId,
                        UnitId = unitId,
                        MinStock = row.MinStock,
                        MaxStock = row.MaxStock,
                        TrackingMethod = tracking,
                        IsActive = true,
                        CreatedAtUtc = now
                    };

                    _context.Products.Add(product);
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                await _auditService.LogAsync(
                    "Import",
                    "Products",
                    "Commit",
                    userId,
                    null,
                    $"Imported {validRows.Count} products successfully",
                    null,
                    cancellationToken);

                return Result<ImportExecutionResultDto>.Success(new ImportExecutionResultDto
                {
                    ImportType = ImportType.Products,
                    Success = true,
                    ImportedCount = validRows.Count,
                    Message = $"Berhasil mengimpor {validRows.Count} produk baru."
                });
            }
            else if (command.ImportType == ImportType.InitialStock)
            {
                var validRows = command.StockRows.Where(r => r.IsValid).ToList();
                if (validRows.Count == 0)
                    return Result<ImportExecutionResultDto>.Failure("Tidak ada baris stok valid yang dapat diimpor.");

                var warehouses = await _context.Warehouses.ToDictionaryAsync(w => w.Code.ToLower(), w => w.Id, cancellationToken);
                var locations = await _context.WarehouseLocations.ToListAsync(cancellationToken);
                var products = await _context.Products.ToDictionaryAsync(p => p.Sku.ToLower(), p => p.Id, cancellationToken);

                foreach (var row in validRows)
                {
                    var whId = warehouses[row.WarehouseCode.ToLower()];
                    var prodId = products[row.Sku.ToLower()];
                    var loc = locations.First(l => l.WarehouseId == whId && string.Equals(l.Code, row.LocationCode, StringComparison.OrdinalIgnoreCase));

                    // Register batch if present
                    if (!string.IsNullOrWhiteSpace(row.BatchNumber))
                    {
                        var batchExists = await _context.Batches.AnyAsync(b => b.ProductId == prodId && b.BatchNumber == row.BatchNumber.Trim(), cancellationToken);
                        if (!batchExists)
                        {
                            _context.Batches.Add(new Batch
                            {
                                ProductId = prodId,
                                BatchNumber = row.BatchNumber.Trim(),
                                ExpiryDate = row.ExpiryDate,
                                IsBlocked = false,
                                CreatedAtUtc = now
                            });
                        }
                    }

                    // Balance update
                    var balance = await _context.InventoryBalances
                        .FirstOrDefaultAsync(b => b.ProductId == prodId &&
                                                  b.WarehouseId == whId &&
                                                  b.WarehouseLocationId == loc.Id &&
                                                  b.BatchNumber == row.BatchNumber, cancellationToken);

                    decimal balanceBefore = 0;
                    if (balance == null)
                    {
                        balance = new InventoryBalance
                        {
                            ProductId = prodId,
                            WarehouseId = whId,
                            WarehouseLocationId = loc.Id,
                            BatchNumber = string.IsNullOrWhiteSpace(row.BatchNumber) ? null : row.BatchNumber.Trim(),
                            OnHand = row.Quantity,
                            Reserved = 0,
                            Status = StockStatus.Available,
                            CreatedAtUtc = now,
                            UpdatedAtUtc = now
                        };
                        _context.InventoryBalances.Add(balance);
                    }
                    else
                    {
                        balanceBefore = balance.OnHand;
                        balance.OnHand += row.Quantity;
                        balance.UpdatedAtUtc = now;
                    }

                    // Valuation update
                    var valuation = await _context.InventoryValuations
                        .FirstOrDefaultAsync(v => v.ProductId == prodId && v.WarehouseId == whId, cancellationToken);

                    if (valuation == null)
                    {
                        valuation = new InventoryValuation
                        {
                            ProductId = prodId,
                            WarehouseId = whId,
                            OnHandQty = row.Quantity,
                            OnHandValue = row.Quantity * row.UnitCost,
                            AvgCost = row.UnitCost,
                            LastCost = row.UnitCost,
                            UpdatedAtUtc = now
                        };
                        _context.InventoryValuations.Add(valuation);
                    }
                    else
                    {
                        var newTotalQty = valuation.OnHandQty + row.Quantity;
                        var newTotalValue = valuation.OnHandValue + (row.Quantity * row.UnitCost);
                        valuation.OnHandQty = newTotalQty;
                        valuation.OnHandValue = newTotalValue;
                        valuation.AvgCost = newTotalQty > 0 ? Math.Round(newTotalValue / newTotalQty, 4) : 0;
                        valuation.LastCost = row.UnitCost;
                        valuation.UpdatedAtUtc = now;
                    }

                    var docNumber = await _sequenceService.GetNextDocumentNumberAsync("GR", now, cancellationToken);

                    // Stock movement
                    var movement = new StockMovement
                    {
                        MovementNumber = docNumber,
                        MovementType = MovementType.GoodsReceipt,
                        ProductId = prodId,
                        WarehouseId = whId,
                        WarehouseLocationId = loc.Id,
                        BatchNumber = string.IsNullOrWhiteSpace(row.BatchNumber) ? null : row.BatchNumber.Trim(),
                        StockStatus = StockStatus.Available,
                        Quantity = row.Quantity,
                        Direction = MovementDirection.In,
                        BalanceBefore = balanceBefore,
                        BalanceAfter = balance.OnHand,
                        UnitCost = row.UnitCost,
                        TotalValue = row.Quantity * row.UnitCost,
                        SourceDocumentType = SourceDocumentType.GoodsReceipt,
                        SourceDocumentId = Guid.NewGuid(),
                        SourceDocumentNumber = $"IMPORT-{docNumber}",
                        Reason = "Impor Saldo Awal",
                        UserId = userId,
                        TimestampUtc = now,
                        Status = MovementStatus.Posted
                    };

                    _context.StockMovements.Add(movement);
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                await _auditService.LogAsync(
                    "Import",
                    "InitialStock",
                    "Commit",
                    userId,
                    null,
                    $"Imported {validRows.Count} initial stock rows successfully",
                    null,
                    cancellationToken);

                return Result<ImportExecutionResultDto>.Success(new ImportExecutionResultDto
                {
                    ImportType = ImportType.InitialStock,
                    Success = true,
                    ImportedCount = validRows.Count,
                    Message = $"Berhasil mengimpor {validRows.Count} baris saldo awal persediaan."
                });
            }

            return Result<ImportExecutionResultDto>.Failure("Tipe impor tidak didukung.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ImportExecutionResultDto>.Failure($"Gagal mengeksekusi impor: {ex.Message}");
        }
    }

    private static List<Dictionary<string, string>> ReadRows(Stream stream, string extension)
    {
        var ext = extension.Trim().ToLower().TrimStart('.');
        if (ext == "xlsx")
        {
            return ReadExcelRows(stream);
        }
        return ReadCsvRows(stream);
    }

    private static List<Dictionary<string, string>> ReadCsvRows(Stream stream)
    {
        var rows = new List<Dictionary<string, string>>();
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim
        });

        if (csv.Read() && csv.ReadHeader())
        {
            var headers = csv.HeaderRecord;
            if (headers != null)
            {
                while (csv.Read())
                {
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var h in headers)
                    {
                        dict[h] = csv.GetField(h) ?? string.Empty;
                    }
                    rows.Add(dict);
                }
            }
        }
        return rows;
    }

    private static List<Dictionary<string, string>> ReadExcelRows(Stream stream)
    {
        var rows = new List<Dictionary<string, string>>();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null) return rows;

        var headerRow = worksheet.FirstRowUsed();
        if (headerRow == null) return rows;

        var headers = new List<string>();
        foreach (var cell in headerRow.CellsUsed())
        {
            headers.Add(cell.GetString().Trim());
        }

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                var cell = row.Cell(i + 1);
                dict[headers[i]] = cell.GetString().Trim();
            }
            rows.Add(dict);
        }
        return rows;
    }

    private static string? GetValue(Dictionary<string, string> dict, params string[] candidateKeys)
    {
        foreach (var key in candidateKeys)
        {
            if (dict.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                return val.Trim();
        }
        return null;
    }
}
