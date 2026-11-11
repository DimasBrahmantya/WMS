using Wms.Application.Common.Models;

namespace Wms.Application.Features.Operations.PurchaseOrders;

public interface IPurchaseOrderService
{
    Task<Result<PaginatedList<PurchaseOrderDto>>> GetPurchaseOrdersAsync(
        PurchaseOrderFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDto>> GetPurchaseOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreatePurchaseOrderAsync(
        CreatePurchaseOrderCommand command,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> UpdatePurchaseOrderAsync(
        Guid id,
        UpdatePurchaseOrderCommand command,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> SubmitForApprovalAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> ApprovePurchaseOrderAsync(
        Guid id,
        Guid approverUserId,
        CancellationToken cancellationToken = default);

    Task<Result> RejectPurchaseOrderAsync(
        Guid id,
        Guid approverUserId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<Result> MarkAsOrderedAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> CancelPurchaseOrderAsync(
        Guid id,
        Guid userId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<Result> RecordReceiptAsync(
        Guid poId,
        List<(Guid poLineId, decimal quantity)> receivedItems,
        CancellationToken cancellationToken = default);

    Task<Result> RollbackReceiptAsync(
        Guid poId,
        List<(Guid poLineId, decimal quantity)> rolledBackItems,
        CancellationToken cancellationToken = default);
}

