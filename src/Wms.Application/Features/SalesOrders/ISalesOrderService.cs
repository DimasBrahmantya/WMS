using Wms.Application.Common.Models;

namespace Wms.Application.Features.SalesOrders;

public interface ISalesOrderService
{
    Task<PaginatedList<SalesOrderDto>> GetSalesOrdersPagedAsync(
        GetSalesOrdersQuery query,
        CancellationToken cancellationToken = default);

    Task<SalesOrderDto?> GetSalesOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<SalesOrderDto>> CreateSalesOrderAsync(
        CreateSalesOrderCommand command,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> ConfirmSalesOrderAsync(
        Guid salesOrderId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> DispatchSalesOrderAsync(
        DispatchSalesOrderCommand command,
        CancellationToken cancellationToken = default);

    Task<Result> CancelSalesOrderAsync(
        Guid salesOrderId,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default);
}

