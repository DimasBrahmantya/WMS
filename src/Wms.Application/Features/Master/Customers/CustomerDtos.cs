namespace Wms.Application.Features.Master.Customers;

public record CustomerDto(
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

public class GetCustomersQuery : Common.Models.PaginationQuery
{
    public bool? IsActive { get; set; }
}

public record CreateCustomerCommand(
    string Code,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxNumber,
    string? Notes);

public record UpdateCustomerCommand(
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

