using Wms.Application.Common.Models;

namespace Wms.Application.Features.Master.Suppliers;

public interface ISupplierService
{
    Task<PaginatedList<SupplierDto>> GetSuppliersAsync(GetSuppliersQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierDto>> GetActiveSuppliersAsync(CancellationToken cancellationToken = default);
    Task<Result<SupplierDto>> GetSupplierByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<SupplierDto>> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateSupplierAsync(UpdateSupplierCommand command, CancellationToken cancellationToken = default);
    Task<Result> DeleteSupplierAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> ToggleSupplierStatusAsync(Guid id, CancellationToken cancellationToken = default);
}

