using Wms.Application.Common.Models;
using Wms.Domain.Enums;

namespace Wms.Application.Features.SalesOrders;

public class SalesOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }

    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public SalesOrderStatus Status { get; set; }
    public string StatusName => Status switch
    {
        SalesOrderStatus.Draft => "Draft",
        SalesOrderStatus.Confirmed => "Dikonfirmasi (Reserved)",
        SalesOrderStatus.Picking => "Sedang Diambil",
        SalesOrderStatus.Dispatched => "Terkirim",
        SalesOrderStatus.Cancelled => "Dibatalkan",
        _ => Status.ToString()
    };

    public decimal TotalAmount { get; set; }
    public int LinesCount => Lines.Count;

    public string? CreatedByUserName { get; set; }
    public string? ConfirmedByUserName { get; set; }
    public string? DispatchedByUserName { get; set; }

    public IReadOnlyList<SalesOrderLineDto> Lines { get; set; } = Array.Empty<SalesOrderLineDto>();
}

public class SalesOrderLineDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;

    public decimal OrderedQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal DispatchedQuantity { get; set; }
    public decimal RemainingQuantity => Math.Max(0, OrderedQuantity - DispatchedQuantity);
    public decimal UnitPrice { get; set; }
    public decimal Subtotal => OrderedQuantity * UnitPrice;

    public string? Notes { get; set; }
}

public class GetSalesOrdersQuery : PaginationQuery
{
    public Guid? WarehouseId { get; set; }
    public Guid? CustomerId { get; set; }
    public SalesOrderStatus? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class CreateSalesOrderCommand
{
    public Guid CustomerId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveryDate { get; set; }
    public string? Notes { get; set; }
    public List<CreateSalesOrderLineCommand> Lines { get; set; } = new();
}

public class CreateSalesOrderLineCommand
{
    public Guid ProductId { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}

public class DispatchSalesOrderCommand
{
    public Guid SalesOrderId { get; set; }
    public Guid UserId { get; set; }
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString();
    public List<DispatchLineAllocationCommand> Allocations { get; set; } = new();
}

public class DispatchLineAllocationCommand
{
    public Guid SalesOrderLineId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public string? BatchNumber { get; set; }
    public decimal Quantity { get; set; }
}

