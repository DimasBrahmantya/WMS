using Wms.Application.Common.Models;

namespace Wms.Application.Features.Operations.Returns;

public interface IStockReturnService
{
    Task<Result<PaginatedList<StockReturnDto>>> GetReturnsAsync(
        StockReturnFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<StockReturnDto>> GetReturnByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreateDraftAsync(
        CreateStockReturnCommand command,
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

    Task<Result> PostAsync(
        Guid id,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<Result> CancelAsync(
        Guid id,
        Guid userId,
        string? reason,
        CancellationToken cancellationToken = default);
}

