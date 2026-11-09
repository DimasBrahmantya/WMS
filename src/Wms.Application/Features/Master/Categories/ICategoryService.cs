using Wms.Application.Common.Models;

namespace Wms.Application.Features.Master.Categories;

public interface ICategoryService
{
    Task<PaginatedList<CategoryDto>> GetCategoriesAsync(GetCategoriesQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryDto>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateCategoryAsync(UpdateCategoryCommand command, CancellationToken cancellationToken = default);
    Task<Result> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> ToggleCategoryStatusAsync(Guid id, CancellationToken cancellationToken = default);
}

