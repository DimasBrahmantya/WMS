using Wms.Domain.Enums;

namespace Wms.Application.Features.Master.Products;

public record ProductDto(
    Guid Id,
    string Sku,
    string? Barcode,
    string Name,
    string? Description,
    string? Brand,
    Guid CategoryId,
    string CategoryName,
    Guid UnitId,
    string UnitName,
    string UnitAbbreviation,
    decimal MinStock,
    decimal? MaxStock,
    TrackingMethod TrackingMethod,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public class GetProductsQuery : Common.Models.PaginationQuery
{
    public Guid? CategoryId { get; set; }
    public bool? IsActive { get; set; }
    public bool? LowStockOnly { get; set; }
    public Guid? WarehouseId { get; set; }
}

public record CreateProductCommand(
    string Sku,
    string? Barcode,
    string Name,
    string? Description,
    string? Brand,
    Guid CategoryId,
    Guid UnitId,
    decimal MinStock,
    decimal? MaxStock,
    TrackingMethod TrackingMethod);

public record UpdateProductCommand(
    Guid Id,
    string Sku,
    string? Barcode,
    string Name,
    string? Description,
    string? Brand,
    Guid CategoryId,
    Guid UnitId,
    decimal MinStock,
    decimal? MaxStock,
    TrackingMethod TrackingMethod,
    bool IsActive);

