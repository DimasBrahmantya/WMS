using Wms.Application.Common.Models;

namespace Wms.Application.Features.Stock.StockOuts;

public interface IStockOutService
{
    Task<PaginatedList<StockOutDto>> GetStockOutsAsync(GetStockOutsQuery query, CancellationToken cancellationToken = default);
    Task<Result<StockOutDto>> GetStockOutByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<StockOutDto>> CreateDraftAsync(CreateStockOutCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateDraftAsync(UpdateStockOutCommand command, CancellationToken cancellationToken = default);
    Task<Result> DeleteDraftAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<Guid>> PostAsync(Guid id, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<Result<Guid>> ReverseAsync(Guid id, string reason, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<decimal> GetAvailableStockAsync(Guid warehouseLocationId, Guid productId, string? batchNumber = null, CancellationToken cancellationToken = default);
}

