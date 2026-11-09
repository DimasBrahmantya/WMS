namespace Wms.Application.Features.Master.Warehouses;

public record WarehouseDto(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    string? ContactPerson,
    bool IsActive,
    int LocationCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record WarehouseLocationDto(
    Guid Id,
    Guid WarehouseId,
    string WarehouseName,
    string WarehouseCode,
    string Code,
    string Name,
    string? Zone,
    string? Aisle,
    string? Rack,
    string? Bin,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public class GetWarehousesQuery : Common.Models.PaginationQuery
{
    public bool? IsActive { get; set; }
}

public class GetWarehouseLocationsQuery : Common.Models.PaginationQuery
{
    public Guid? WarehouseId { get; set; }
    public bool? IsActive { get; set; }
    public string? Zone { get; set; }
}

public record CreateWarehouseCommand(
    string Code,
    string Name,
    string? Address,
    string? ContactPerson);

public record UpdateWarehouseCommand(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    string? ContactPerson,
    bool IsActive);

public record CreateWarehouseLocationCommand(
    Guid WarehouseId,
    string Code,
    string Name,
    string? Zone,
    string? Aisle,
    string? Rack,
    string? Bin);

public record UpdateWarehouseLocationCommand(
    Guid Id,
    string Code,
    string Name,
    string? Zone,
    string? Aisle,
    string? Rack,
    string? Bin,
    bool IsActive);

