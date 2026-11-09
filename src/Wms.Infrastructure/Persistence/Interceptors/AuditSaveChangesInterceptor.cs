using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Common;
using Wms.Domain.Entities.System;

namespace Wms.Infrastructure.Persistence.Interceptors;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IUserContext _userContext;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public AuditSaveChangesInterceptor(IUserContext userContext, ICorrelationIdAccessor correlationIdAccessor)
    {
        _userContext = userContext;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null)
        {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var utcNow = DateTime.UtcNow;
        var userId = _userContext.UserId;
        var userName = _userContext.Username;
        var correlationId = _correlationIdAccessor.CorrelationId;

        var auditEntries = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog)
            {
                continue;
            }

            // Update timestamp & user auditing
            if (entry.Entity is AuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAtUtc = utcNow;
                    auditable.CreatedBy ??= userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAtUtc = utcNow;
                    auditable.UpdatedBy = userId;
                }
            }

            // Create AuditLog entry
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                var entityType = entry.Entity.GetType().Name;
                var action = entry.State switch
                {
                    EntityState.Added => "Create",
                    EntityState.Modified => "Update",
                    EntityState.Deleted => "Delete",
                    _ => "Unknown"
                };

                var entityId = entry.Property("Id")?.CurrentValue?.ToString();

                var oldValues = new Dictionary<string, object?>();
                var newValues = new Dictionary<string, object?>();
                var changedProperties = new List<string>();

                foreach (var prop in entry.Properties)
                {
                    var propName = prop.Metadata.Name;
                    if (propName.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                        propName.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                        propName.Contains("SecurityStamp", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Protect passwords and secrets
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            newValues[propName] = prop.CurrentValue;
                            break;
                        case EntityState.Deleted:
                            oldValues[propName] = prop.OriginalValue;
                            break;
                        case EntityState.Modified:
                            if (prop.IsModified)
                            {
                                changedProperties.Add(propName);
                                oldValues[propName] = prop.OriginalValue;
                                newValues[propName] = prop.CurrentValue;
                            }
                            break;
                    }
                }

                auditEntries.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    UserName = userName,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    TimestampUtc = utcNow,
                    OldValues = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null,
                    NewValues = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null,
                    AffectedColumns = changedProperties.Count > 0 ? string.Join(",", changedProperties) : null,
                    CorrelationId = correlationId
                });
            }
        }

        if (auditEntries.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditEntries);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
