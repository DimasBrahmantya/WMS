using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Audit;
using Wms.Domain.Entities.System;

namespace Wms.Infrastructure.Services.Audit;

public class AppAuditService : IAppAuditService
{
    private readonly IWmsDbContext _dbContext;

    public AppAuditService(IWmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(
        string entityType,
        string entityId,
        string action,
        Guid? userId,
        string? oldValues = null,
        string? newValues = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = userId,
            OldValues = oldValues,
            NewValues = newValues,
            Reason = reason,
            TimestampUtc = DateTime.UtcNow
        };

        _dbContext.AuditLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
