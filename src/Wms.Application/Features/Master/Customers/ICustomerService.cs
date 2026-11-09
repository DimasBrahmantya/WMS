using Wms.Application.Common.Models;

namespace Wms.Application.Features.Master.Customers;

public interface ICustomerService
{
    Task<PaginatedList<CustomerDto>> GetCustomersAsync(GetCustomersQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerDto>> GetActiveCustomersAsync(CancellationToken cancellationToken = default);
    Task<Result<CustomerDto>> GetCustomerByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<CustomerDto>> CreateCustomerAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateCustomerAsync(UpdateCustomerCommand command, CancellationToken cancellationToken = default);
    Task<Result> DeleteCustomerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> ToggleCustomerStatusAsync(Guid id, CancellationToken cancellationToken = default);
}

