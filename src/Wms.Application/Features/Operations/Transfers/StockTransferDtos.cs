using Wms.Application.Common.Models;
using Wms.Domain.Enums;

namespace Wms.Application.Features.Operations.Transfers;

public record StockTransferDto
{
    public Guid Id { get; init; }
    public string TransferNumber { get; init; } = string.Empty;
    public DateTime TransferDate { get; init; }

    public Guid OriginWarehouseId { get; init; }
    public string OriginWarehouseName { get; init; } = string.Empty;

    public Guid DestinationWarehouseId { get; init; }
    public string DestinationWarehouseName { get; init; } = string.Empty;

    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public StockTransferStatus Status { get; init; }

    public Guid CreatedByUserId { get; init; }
    public string CreatedByUserName { get; init; } = string.Empty;

    public Guid? ApprovedByUserId { get; init; }
    public string? ApprovedByUserName { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }
    public string? RejectionReason { get; init; }

    public Guid? DispatchedByUserId { get; init; }
    public string? DispatchedByUserName { get; init; }
    public DateTime? DispatchedAtUtc { get; init; }

    public Guid? ReceivedByUserId { get; init; }
    public string? ReceivedByUserName { get; init; }
    public DateTime? ReceivedAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public string ConcurrencyToken { get; init; } = string.Empty;

    public List<StockTransferLineDto> Lines { get; init; } = new();
}

public record StockTransferLineDto
{
    public Guid Id { get; init; }
    public Guid StockTransferId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;

    public Guid? OriginLocationId { get; init; }
    public string? OriginLocationName { get; init; }

    public Guid? DestinationLocationId { get; init; }
    public string? DestinationLocationName { get; init; }

    public decimal QuantityRequested { get; init; }
    public decimal QuantityDispatched { get; init; }
    public decimal QuantityReceived { get; init; }
    public decimal RemainingQuantity { get; init; }
    public string? Notes { get; init; }
}

public class CreateStockTransferCommand
{
    public Guid OriginWarehouseId { get; set; }
    public Guid DestinationWarehouseId { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public List<CreateStockTransferLineDto> Lines { get; set; } = new();
}

public class CreateStockTransferLineDto
{
    public Guid ProductId { get; set; }
    public Guid? OriginLocationId { get; set; }
    public Guid? DestinationLocationId { get; set; }
    public decimal QuantityRequested { get; set; }
    public string? Notes { get; set; }
}

public class ReceiveStockTransferCommand
{
    public List<ReceiveStockTransferLineDto> Lines { get; set; } = new();
    public string? Notes { get; set; }
}

public class ReceiveStockTransferLineDto
{
    public Guid TransferLineId { get; set; }
    public Guid DestinationLocationId { get; set; }
    public decimal QuantityReceived { get; set; }
}

public class StockTransferFilterQuery : PaginationQuery
{
    public string? SearchTerm { get; set; }
    public Guid? OriginWarehouseId { get; set; }
    public Guid? DestinationWarehouseId { get; set; }
    public StockTransferStatus? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
