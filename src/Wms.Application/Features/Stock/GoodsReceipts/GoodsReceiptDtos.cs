using Wms.Domain.Enums;

namespace Wms.Application.Features.Stock.GoodsReceipts;

public record GoodsReceiptDto
{
    public Guid Id { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public DateTime DocumentDate { get; init; }
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = string.Empty;
    public Guid? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public string? PurchaseOrderNumber { get; init; }
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

    public IReadOnlyList<GoodsReceiptLineDto> Lines { get; init; } = Array.Empty<GoodsReceiptLineDto>();
}

public record GoodsReceiptLineDto
{
    public Guid Id { get; init; }
    public Guid GoodsReceiptId { get; init; }
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
    public DateTime? ExpiryDate { get; init; }
    public string? Notes { get; init; }
}

public class CreateGoodsReceiptCommand
{
    public DateTime DocumentDate { get; set; } = DateTime.UtcNow;
    public Guid WarehouseId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public string? Notes { get; set; }
    public List<CreateGoodsReceiptLineDto> Lines { get; set; } = new();
}

public class CreateGoodsReceiptLineDto
{
    public Guid ProductId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }
}

public class UpdateGoodsReceiptCommand
{
    public Guid Id { get; set; }
    public DateTime DocumentDate { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public string? Notes { get; set; }
    public List<CreateGoodsReceiptLineDto> Lines { get; set; } = new();
}

public record GetGoodsReceiptsQuery
{
    public string? SearchTerm { get; init; }
    public Guid? WarehouseId { get; init; }
    public DocumentStatus? Status { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}
