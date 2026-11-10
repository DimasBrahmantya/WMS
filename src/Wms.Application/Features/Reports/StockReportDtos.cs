using Wms.Application.Common.Models;

namespace Wms.Application.Features.Reports;

public class StockReportQuery : PaginationQuery
{
    public Guid? WarehouseId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? ProductId { get; set; }
    /// <summary>
    /// Filter status: "all", "safe", "low", "out"
    /// </summary>
    public string? StockStatus { get; set; } = "all";
}

public class StockReportItemDto
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;

    public Guid WarehouseLocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }

    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public decimal Available => OnHand - Reserved;

    public decimal MinStock { get; set; }
    public decimal? MaxStock { get; set; }

    public string Status => OnHand switch
    {
        <= 0 => "Habis",
        _ when OnHand <= MinStock => "Rendah",
        _ => "Aman"
    };
}

public class StockReportSummaryDto
{
    public int TotalSkuCount { get; set; }
    public int TotalLowStockCount { get; set; }
    public int TotalOutOfStockCount { get; set; }
    public List<Features.Dashboard.UnitQuantityDto> UnitTotals { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string ReportType => "Saldo Saat Ini (Real-Time)";
    public string Description => "Data persediaan fisik dan reservasi terkini di seluruh gudang dan rak penyimpanan.";
}

