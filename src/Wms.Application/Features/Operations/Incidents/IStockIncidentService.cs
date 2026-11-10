using Wms.Application.Common.Models;

namespace Wms.Application.Features.Operations.Incidents;

public interface IStockIncidentService
{
    Task<Result<PaginatedList<StockIncidentDto>>> GetIncidentsAsync(
        StockIncidentFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<StockIncidentDto>> GetIncidentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> ReportIncidentAsync(
        CreateStockIncidentCommand command,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> UpdateInvestigationAsync(
        Guid id,
        string investigationNotes,
        Guid investigatorUserId,
        CancellationToken cancellationToken = default);

    Task<Result> ApproveAsync(
        Guid id,
        Guid approverUserId,
        CancellationToken cancellationToken = default);

    Task<Result> RejectAsync(
        Guid id,
        Guid approverUserId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<Result> ResolveAsync(
        Guid id,
        string actionTaken,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<Result> CancelAsync(
        Guid id,
        Guid userId,
        string? reason,
        CancellationToken cancellationToken = default);
}
