using Wms.Application.Common.Models;

namespace Wms.Application.Features.Master.Warehouses;

public interface IWarehouseService
{
    // Warehouses
    Task<PaginatedList<WarehouseDto>> GetWarehousesAsync(GetWarehousesQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WarehouseDto>> GetActiveWarehousesAsync(CancellationToken cancellationToken = default);
    Task<Result<WarehouseDto>> GetWarehouseByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<WarehouseDto>> CreateWarehouseAsync(CreateWarehouseCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateWarehouseAsync(UpdateWarehouseCommand command, CancellationToken cancellationToken = default);
    Task<Result> DeleteWarehouseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> ToggleWarehouseStatusAsync(Guid id, CancellationToken cancellationToken = default);

    // Locations
    Task<PaginatedList<WarehouseLocationDto>> GetLocationsAsync(GetWarehouseLocationsQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WarehouseLocationDto>> GetActiveLocationsByWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default);
    Task<Result<WarehouseLocationDto>> GetLocationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<WarehouseLocationDto>> CreateLocationAsync(CreateWarehouseLocationCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateLocationAsync(UpdateWarehouseLocationCommand command, CancellationToken cancellationToken = default);
    Task<Result> DeleteLocationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> ToggleLocationStatusAsync(Guid id, CancellationToken cancellationToken = default);
}

