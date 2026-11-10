using Wms.Application.Common.Models;

namespace Wms.Application.Features.Audit;

public interface IAuditLogService
{
    Task<PaginatedList<AuditLogDto>> GetAuditLogsAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);

    Task<AuditLogSummaryDto> GetAuditSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetEntityTypesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetActionsAsync(
        CancellationToken cancellationToken = default);
}

