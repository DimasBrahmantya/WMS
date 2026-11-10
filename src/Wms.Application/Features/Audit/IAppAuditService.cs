namespace Wms.Application.Features.Audit;

public interface IAppAuditService
{
    Task LogAsync(
        string entityType,
        string entityId,
        string action,
        Guid? userId,
        string? oldValues = null,
        string? newValues = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
