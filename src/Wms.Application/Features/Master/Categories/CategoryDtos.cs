namespace Wms.Application.Features.Master.Categories;

public record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int ProductCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public class GetCategoriesQuery : Common.Models.PaginationQuery
{
    public bool? IsActive { get; set; }
}

public record CreateCategoryCommand(
    string Name,
    string? Description);

public record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive);

