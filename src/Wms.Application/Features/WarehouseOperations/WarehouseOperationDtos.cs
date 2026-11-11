namespace Wms.Application.Features.WarehouseOperations;

public class PutawaySuggestionDto
{
    public Guid WarehouseLocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class PickingListDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public IReadOnlyList<PickingListLineDto> Lines { get; set; } = Array.Empty<PickingListLineDto>();
}

public class PickingListLineDto
{
    public Guid ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantityOrdered { get; set; }
    public IReadOnlyList<PickingItemAllocationDto> Allocations { get; set; } = Array.Empty<PickingItemAllocationDto>();
}

public class PickingItemAllocationDto
{
    public Guid WarehouseLocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }
    public decimal QuantityToPick { get; set; }
}

