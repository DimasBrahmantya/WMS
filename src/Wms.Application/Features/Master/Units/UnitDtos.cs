namespace Wms.Application.Features.Master.Units;

public record UnitDto(
    Guid Id,
    string Name,
    string Abbreviation,
    bool IsActive,
    int ProductCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public class GetUnitsQuery : Common.Models.PaginationQuery
{
    public bool? IsActive { get; set; }
}

public record CreateUnitCommand(
    string Name,
    string Abbreviation);

public record UpdateUnitCommand(
    Guid Id,
    string Name,
    string Abbreviation,
    bool IsActive);

