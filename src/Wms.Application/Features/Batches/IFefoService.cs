namespace Wms.Application.Features.Batches;

public interface IFefoService
{
    Task<IReadOnlyList<BatchAllocationResultDto>> AllocateFefoAsync(
        Guid productId,
        Guid warehouseId,
        decimal requiredQuantity,
        bool includeReserved = false,
        CancellationToken cancellationToken = default);

    Task<bool> IsBatchExpiredOrBlockedAsync(
        Guid productId,
        string? batchNumber,
        CancellationToken cancellationToken = default);
}

