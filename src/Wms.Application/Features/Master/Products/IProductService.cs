using Wms.Application.Common.Models;

namespace Wms.Application.Features.Master.Products;

public interface IProductService
{
    Task<PaginatedList<ProductDto>> GetProductsAsync(GetProductsQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductDto>> GetActiveProductsAsync(CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> CreateProductAsync(CreateProductCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateProductAsync(UpdateProductCommand command, CancellationToken cancellationToken = default);
    Task<Result> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> ToggleProductStatusAsync(Guid id, CancellationToken cancellationToken = default);
}

