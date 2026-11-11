namespace Wms.Application.Features.WarehouseOperations;

public interface IPutawayService
{
    Task<PutawaySuggestionDto?> SuggestPutawayLocationAsync(
        Guid productId,
        Guid warehouseId,
        string? batchNumber = null,
        CancellationToken cancellationToken = default);
}

