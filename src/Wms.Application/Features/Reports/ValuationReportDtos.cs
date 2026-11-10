using Wms.Application.Common.Models;
using Wms.Application.Features.Dashboard;

namespace Wms.Application.Features.Reports;

public enum ValuationReportMode
{
    Current = 1,
    Historical = 2
}

public class ValuationReportQuery : PaginationQuery
{
    public Guid? WarehouseId { get; set; }
    public Guid? CategoryId { get; set; }
    public ValuationReportMode Mode { get; set; } = ValuationReportMode.Current;
    public DateTime? AsOfDate { get; set; }
}

public class ValuationReportItemDto
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

    public decimal OnHandQty { get; set; }
    public decimal AvgCost { get; set; }
    public decimal OnHandValue { get; set; }
    public decimal LastCost { get; set; }
    public DateTime LastUpdatedAtUtc { get; set; }
}

public class ValuationReportSummaryDto
{
    public string Method => "Rata-Rata Bergerak (Moving Average)";
    public ValuationReportMode Mode { get; set; } = ValuationReportMode.Current;
    public DateTime CalculationDateUtc { get; set; } = DateTime.UtcNow;
    public decimal TotalValuationValue { get; set; }
    public int TotalValuedItems { get; set; }
    public List<WarehouseValuationDto> WarehouseBreakdown { get; set; } = new();

    public string Description => Mode == ValuationReportMode.Current
        ? "Valuasi persediaan saat ini (real-time) dihitung menggunakan metode biaya rata-rata bergerak (moving average) per barang per gudang."
        : "Valuasi persediaan historis direkonstruksi secara kronologis dari ledger pergerakan stok (stock movements) hingga tanggal batas perhitungan.";
}

