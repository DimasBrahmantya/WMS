using Wms.Application.Common.Models;

namespace Wms.Application.Features.Operations.Transfers;

public interface IStockTransferService
{
    Task<Result<PaginatedList<StockTransferDto>>> GetStockTransfersAsync(
        StockTransferFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<StockTransferDto>> GetStockTransferByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreateDraftAsync(
        CreateStockTransferCommand command,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> SubmitForApprovalAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> ApproveAsync(
        Guid id,
        Guid approverUserId,
        CancellationToken cancellationToken = default);

    Task<Result> RejectAsync(
        Guid id,
        Guid approverUserId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<Result> DispatchAsync(
        Guid id,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<Result> ReceiveAsync(
        Guid id,
        ReceiveStockTransferCommand command,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<Result> CancelAsync(
        Guid id,
        Guid userId,
        string? reason,
        CancellationToken cancellationToken = default);
}

