using Wms.Application.Common.Models;

namespace Wms.Application.Features.Batches;

public interface IBatchService
{
    Task<PaginatedList<BatchDto>> GetBatchesPagedAsync(
        GetBatchesQuery query,
        CancellationToken cancellationToken = default);

    Task<BatchDto?> GetBatchByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<BatchDto>> CreateOrUpdateBatchAsync(
        CreateBatchCommand command,
        CancellationToken cancellationToken = default);

    Task<Result> SetBlockStatusAsync(
        UpdateBatchBlockStatusCommand command,
        CancellationToken cancellationToken = default);
}

