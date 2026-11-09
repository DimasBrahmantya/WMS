namespace Wms.Application.Features.Master.Suppliers;

public record SupplierDto(
    Guid Id,
    string Code,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxNumber,
    string? Notes,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public class GetSuppliersQuery : Common.Models.PaginationQuery
{
    public bool? IsActive { get; set; }
}

public record CreateSupplierCommand(
    string Code,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxNumber,
    string? Notes);

public record UpdateSupplierCommand(
    Guid Id,
    string Code,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxNumber,
    string? Notes,
    bool IsActive);

