using Wms.Application.Common.Models;
using Wms.Domain.Enums;

namespace Wms.Application.Features.Operations.Returns;

public record StockReturnDto
{
    public Guid Id { get; init; }
    public string ReturnNumber { get; init; } = string.Empty;
    public DateTime ReturnDate { get; init; }

    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = string.Empty;

    public StockReturnType ReturnType { get; init; }

    public Guid? SupplierId { get; init; }
    public string? SupplierName { get; init; }

    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }

    public string? ReferenceDocumentNumber { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }

    public StockReturnStatus Status { get; init; }

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

    public List<StockReturnLineDto> Lines { get; init; } = new();
}

public record StockReturnLineDto
{
    public Guid Id { get; init; }
    public Guid StockReturnId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;

    public Guid? LocationId { get; init; }
    public string? LocationName { get; init; }

    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public StockStatus TargetStockStatus { get; init; }
    public string? Notes { get; init; }
}

public class CreateStockReturnCommand
{
    public Guid WarehouseId { get; set; }
    public StockReturnType ReturnType { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? ReferenceDocumentNumber { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public List<CreateStockReturnLineDto> Lines { get; set; } = new();
}

public class CreateStockReturnLineDto
{
    public Guid ProductId { get; set; }
    public Guid? LocationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public StockStatus TargetStockStatus { get; set; } = StockStatus.Available;
    public string? Notes { get; set; }
}

public class StockReturnFilterQuery : PaginationQuery
{
    public string? SearchTerm { get; set; }
    public Guid? WarehouseId { get; set; }
    public StockReturnType? ReturnType { get; set; }
    public StockReturnStatus? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
