using Wms.Application.Common.Models;
using Wms.Domain.Enums;

namespace Wms.Application.Features.Operations.PurchaseOrders;

public record PurchaseOrderDto
{
    public Guid Id { get; init; }
    public string PoNumber { get; init; } = string.Empty;
    public DateTime OrderDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }

    public Guid SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;

    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = string.Empty;

    public string? Notes { get; init; }
    public PurchaseOrderStatus Status { get; init; }
    public decimal TotalAmount { get; init; }

    public Guid CreatedByUserId { get; init; }
    public string CreatedByUserName { get; init; } = string.Empty;

    public Guid? ApprovedByUserId { get; init; }
    public string? ApprovedByUserName { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }
    public string? RejectionReason { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public string ConcurrencyToken { get; init; } = string.Empty;

    public List<PurchaseOrderLineDto> Lines { get; init; } = new();
}

public record PurchaseOrderLineDto
{
    public Guid Id { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;

    public decimal OrderedQuantity { get; init; }
    public decimal ReceivedQuantity { get; init; }
    public decimal RemainingQuantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Subtotal { get; init; }
    public string? Notes { get; init; }
}

public class CreatePurchaseOrderCommand
{
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderLineItemCommand> Lines { get; set; } = new();
}

public class UpdatePurchaseOrderCommand
{
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderLineItemCommand> Lines { get; set; } = new();
}

public class PurchaseOrderLineItemCommand
{
    public Guid? Id { get; set; }
    public Guid ProductId { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}

public class PurchaseOrderFilterQuery : PaginationQuery
{
    public string? SearchTerm { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? WarehouseId { get; set; }
    public PurchaseOrderStatus? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
