namespace Wms.Application.Features.WarehouseOperations;

public interface IPickingService
{
    Task<PickingListDto> GeneratePickingListForSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default);
}

