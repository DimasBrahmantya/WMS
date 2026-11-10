using Wms.Application.Common.Models;

namespace Wms.Application.Features.Stock.GoodsReceipts;

public interface IGoodsReceiptService
{
    Task<PaginatedList<GoodsReceiptDto>> GetGoodsReceiptsAsync(GetGoodsReceiptsQuery query, CancellationToken cancellationToken = default);
    Task<Result<GoodsReceiptDto>> GetGoodsReceiptByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<GoodsReceiptDto>> CreateDraftAsync(CreateGoodsReceiptCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateDraftAsync(UpdateGoodsReceiptCommand command, CancellationToken cancellationToken = default);
    Task<Result> DeleteDraftAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<Guid>> PostAsync(Guid id, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<Result<Guid>> ReverseAsync(Guid id, string reason, string idempotencyKey, CancellationToken cancellationToken = default);
}

