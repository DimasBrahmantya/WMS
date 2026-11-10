using Wms.Domain.Enums;

namespace Wms.Application.Features.Dashboard;

public class DashboardFilterDto
{
    public Guid? WarehouseId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int InactiveThresholdDays { get; set; } = 30;
}

public class UnitQuantityDto
{
    public string UnitName { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
}

public class WarehouseValuationDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public decimal TotalValue { get; set; }
    public decimal TotalQuantity { get; set; }
}

public class CategoryStockDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public decimal TotalQuantity { get; set; }
}

public class MovementTrendDto
{
    public DateTime Date { get; set; }
    public string DateLabel { get; set; } = string.Empty;
    public decimal InQty { get; set; }
    public decimal OutQty { get; set; }
    public int TransactionCount { get; set; }
}

public class TopMovingProductDto
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public decimal TotalOutQty { get; set; }
    public decimal TotalOutValue { get; set; }
}

public class DeadStockProductDto
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public decimal CurrentOnHand { get; set; }
    public DateTime? LastMovementUtc { get; set; }
    public int DaysInactive { get; set; }
}

public class RecentMovementItemDto
{
    public Guid Id { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public string SourceDocumentNumber { get; set; } = string.Empty;
    public SourceDocumentType SourceDocumentType { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public MovementDirection Direction { get; set; }
    public MovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public MovementStatus Status { get; set; }
}

public class DashboardSummaryDto
{
    public int TotalActiveSku { get; set; }
    public int TotalActiveWarehouses { get; set; }
    public decimal TotalValuation { get; set; }
    public List<UnitQuantityDto> QuantitiesByUnit { get; set; } = new();
    public List<WarehouseValuationDto> ValuationByWarehouse { get; set; } = new();
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }

    public int TodayGoodsReceiptsCount { get; set; }
    public int TodayStockOutsCount { get; set; }
    public int TodayMovementCount { get; set; }

    // Indikator Fase 2 (Belum Tersedia)
    public int PendingPurchaseOrdersCount { get; set; } = 0;
    public int PendingTransfersCount { get; set; } = 0;
    public int ReportedIncidentsCount { get; set; } = 0;

    public List<CategoryStockDto> CategoryDistribution { get; set; } = new();
    public List<MovementTrendDto> MovementTrends { get; set; } = new();
    public List<TopMovingProductDto> TopMovingProducts { get; set; } = new();
    public List<DeadStockProductDto> DeadStockProducts { get; set; } = new();
    public List<RecentMovementItemDto> RecentMovements { get; set; } = new();

    public Guid? FilterWarehouseId { get; set; }
    public string? FilterWarehouseName { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
}
