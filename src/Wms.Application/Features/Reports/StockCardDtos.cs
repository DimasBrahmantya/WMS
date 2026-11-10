using Wms.Domain.Enums;

namespace Wms.Application.Features.Reports;

public class StockCardQuery
{
    public Guid ProductId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public MovementType? MovementType { get; set; }
    public MovementDirection? Direction { get; set; }
}

public class StockCardLineDto
{
    public Guid MovementId { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }

    public string SourceDocumentNumber { get; set; } = string.Empty;
    public SourceDocumentType SourceDocumentType { get; set; }

    public MovementType MovementType { get; set; }
    public MovementDirection Direction { get; set; }

    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public decimal RunningBalance { get; set; }

    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }

    public string WarehouseName { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public MovementStatus Status { get; set; }
}

public class StockCardReportDto
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public decimal MinStock { get; set; }

    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }

    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }

    /// <summary>
    /// Saldo awal historis yang direkonstruksi dari ledger stock_movements sebelum rentang tanggal terpilih.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    public decimal TotalQuantityIn { get; set; }
    public decimal TotalQuantityOut { get; set; }

    /// <summary>
    /// Saldo akhir historis di akhir rentang tanggal terpilih (OpeningBalance + In - Out).
    /// </summary>
    public decimal ClosingBalance { get; set; }

    /// <summary>
    /// Saldo fisik saat ini (real-time) dari tabel inventory_balances untuk membedakan saldo saat ini vs historis.
    /// </summary>
    public decimal CurrentRealTimeBalance { get; set; }

    public List<StockCardLineDto> Lines { get; set; } = new();
}

