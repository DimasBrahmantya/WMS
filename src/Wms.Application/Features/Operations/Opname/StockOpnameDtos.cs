using Wms.Application.Common.Models;
using Wms.Domain.Enums;

namespace Wms.Application.Features.Operations.Opname;

public record StockOpnameSessionDto
{
    public Guid Id { get; init; }
    public string SessionNumber { get; init; } = string.Empty;
    public DateTime CutoffDate { get; init; }

    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = string.Empty;

    public Guid? CategoryId { get; init; }
    public string? CategoryName { get; init; }

    public Guid? LocationId { get; init; }
    public string? LocationName { get; init; }

    public string? Notes { get; init; }
    public StockOpnameStatus Status { get; init; }

    public decimal TotalVarianceQuantity { get; init; }
    public decimal TotalVarianceValue { get; init; }

    public Guid CreatedByUserId { get; init; }
    public string CreatedByUserName { get; init; } = string.Empty;

    public Guid? ApprovedByUserId { get; init; }
    public string? ApprovedByUserName { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }
    public string? RejectionReason { get; init; }

    public Guid? PostedByUserId { get; init; }
    public string? PostedByUserName { get; init; }
    public DateTime? PostedAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public string ConcurrencyToken { get; init; } = string.Empty;

    public List<StockOpnameLineDto> Lines { get; init; } = new();
}

public record StockOpnameLineDto
{
    public Guid Id { get; init; }
    public Guid StockOpnameSessionId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;

    public Guid? LocationId { get; init; }
    public string? LocationName { get; init; }

    public decimal SystemSnapshotQuantity { get; init; }
    public decimal SystemSnapshotAvgCost { get; init; }

    public decimal? CountedQuantity { get; init; }
    public decimal? RecountQuantity { get; init; }
    public decimal FinalCountQuantity { get; init; }
    public decimal VarianceQuantity { get; init; }
    public decimal VarianceValue { get; init; }

    public string? Notes { get; init; }
}

public class CreateStockOpnameSessionCommand
{
    public Guid WarehouseId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? LocationId { get; set; }
    public string? Notes { get; set; }
}

public class RecordOpnameCountCommand
{
    public List<RecordOpnameLineCountDto> Lines { get; set; } = new();
}

public class RecordOpnameLineCountDto
{
    public Guid LineId { get; set; }
    public decimal? CountedQuantity { get; set; }
    public decimal? RecountQuantity { get; set; }
    public string? Notes { get; set; }
}

public class StockOpnameFilterQuery : PaginationQuery
{
    public string? SearchTerm { get; set; }
    public Guid? WarehouseId { get; set; }
    public StockOpnameStatus? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
