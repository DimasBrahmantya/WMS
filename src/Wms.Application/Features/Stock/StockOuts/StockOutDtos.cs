using Wms.Domain.Enums;

namespace Wms.Application.Features.Stock.StockOuts;

public record StockOutDto
{
    public Guid Id { get; init; }
    public string IssueNumber { get; init; } = string.Empty;
    public DateTime DocumentDate { get; init; }
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = string.Empty;
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? Recipient { get; init; }
    public StockOutType IssueType { get; init; }
    public string? Notes { get; init; }
    public DocumentStatus Status { get; init; }

    public Guid CreatedByUserId { get; init; }
    public string CreatedByUserName { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }

    public Guid? PostedByUserId { get; init; }
    public string? PostedByUserName { get; init; }
    public DateTime? PostedAtUtc { get; init; }

    public Guid? ReversedByUserId { get; init; }
    public string? ReversedByUserName { get; init; }
    public DateTime? ReversedAtUtc { get; init; }
    public string? ReversalReason { get; init; }

    public decimal TotalQuantity { get; init; }
    public decimal TotalAmount { get; init; }

    public IReadOnlyList<StockOutLineDto> Lines { get; init; } = Array.Empty<StockOutLineDto>();
}

public record StockOutLineDto
{
    public Guid Id { get; init; }
    public Guid StockOutId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;
    public Guid WarehouseLocationId { get; init; }
    public string WarehouseLocationCode { get; init; } = string.Empty;
    public string WarehouseLocationName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public decimal TotalCost { get; init; }
    public string? BatchNumber { get; init; }
    public string? Notes { get; init; }
    public decimal AvailableStock { get; init; }
}

public class CreateStockOutCommand
{
    public DateTime DocumentDate { get; set; } = DateTime.UtcNow;
    public Guid WarehouseId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Recipient { get; set; }
    public StockOutType IssueType { get; set; } = StockOutType.CustomerDelivery;
    public string? Notes { get; set; }
    public List<CreateStockOutLineDto> Lines { get; set; } = new();
}

public class CreateStockOutLineDto
{
    public Guid ProductId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public decimal Quantity { get; set; }
    public string? BatchNumber { get; set; }
    public string? Notes { get; set; }
}

public class UpdateStockOutCommand
{
    public Guid Id { get; set; }
    public DateTime DocumentDate { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Recipient { get; set; }
    public StockOutType IssueType { get; set; }
    public string? Notes { get; set; }
    public List<CreateStockOutLineDto> Lines { get; set; } = new();
}

public record GetStockOutsQuery
{
    public string? SearchTerm { get; init; }
    public Guid? WarehouseId { get; init; }
    public DocumentStatus? Status { get; init; }
    public StockOutType? IssueType { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}
