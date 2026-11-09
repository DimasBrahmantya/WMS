using Wms.Application.Common.Models;

namespace Wms.Application.Features.Master.Units;

public interface IUnitService
{
    Task<PaginatedList<UnitDto>> GetUnitsAsync(GetUnitsQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UnitDto>> GetActiveUnitsAsync(CancellationToken cancellationToken = default);
    Task<Result<UnitDto>> GetUnitByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<UnitDto>> CreateUnitAsync(CreateUnitCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateUnitAsync(UpdateUnitCommand command, CancellationToken cancellationToken = default);
    Task<Result> DeleteUnitAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> ToggleUnitStatusAsync(Guid id, CancellationToken cancellationToken = default);
}

