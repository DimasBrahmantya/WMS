using Wms.Application.Common.Models;

namespace Wms.Application.Features.Batches;

public class BatchDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public decimal TotalOnHand { get; set; }
    public decimal TotalReserved { get; set; }
    public decimal AvailableQuantity => Math.Max(0, TotalOnHand - TotalReserved);
    public int? DaysToExpiry { get; set; }
    public bool IsExpired { get; set; }
    public bool IsNearExpiry { get; set; }
}

public class GetBatchesQuery
{
    public Guid? WarehouseId { get; set; }
    public Guid? ProductId { get; set; }
    public string? SearchTerm { get; set; }
    public bool? OnlyNearExpiry { get; set; }
    public bool? OnlyBlocked { get; set; }
    public bool? OnlyExpired { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class CreateBatchCommand
{
    public Guid ProductId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}

public class UpdateBatchBlockStatusCommand
{
    public Guid BatchId { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
}

public class BatchAllocationResultDto
{
    public string BatchNumber { get; set; } = string.Empty;
    public Guid WarehouseLocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public decimal AllocatedQuantity { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}

