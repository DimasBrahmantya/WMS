using Wms.Application.Common.Models;

namespace Wms.Application.Features.Reports;

public interface IReportService
{
    Task<PaginatedList<StockReportItemDto>> GetCurrentStockReportAsync(
        StockReportQuery query,
        CancellationToken cancellationToken = default);

    Task<StockReportSummaryDto> GetStockReportSummaryAsync(
        StockReportQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<StockCardReportDto>> GetStockCardReportAsync(
        StockCardQuery query,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<ValuationReportItemDto>> GetValuationReportAsync(
        ValuationReportQuery query,
        CancellationToken cancellationToken = default);

    Task<ValuationReportSummaryDto> GetValuationReportSummaryAsync(
        ValuationReportQuery query,
        CancellationToken cancellationToken = default);
}

