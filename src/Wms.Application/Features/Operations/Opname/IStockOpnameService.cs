using Wms.Application.Common.Models;

namespace Wms.Application.Features.Operations.Opname;

public interface IStockOpnameService
{
    Task<Result<PaginatedList<StockOpnameSessionDto>>> GetSessionsAsync(
        StockOpnameFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<StockOpnameSessionDto>> GetSessionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreateSessionAsync(
        CreateStockOpnameSessionCommand command,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> StartCountingAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> RecordCountsAsync(
        Guid id,
        RecordOpnameCountCommand command,
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

    Task<Result> PostAdjustmentAsync(
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
