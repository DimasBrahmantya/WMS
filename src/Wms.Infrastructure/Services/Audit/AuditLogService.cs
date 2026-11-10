using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Audit;
using Wms.Domain.Common;

namespace Wms.Infrastructure.Services.Audit;

public class AuditLogService : IAuditLogService
{
    private readonly IWmsDbContext _dbContext;

    public AuditLogService(IWmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AuditLogDto>> GetAuditLogsAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = _dbContext.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            dbQuery = dbQuery.Where(a => a.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            dbQuery = dbQuery.Where(a => a.EntityType == query.EntityType);
        }

        if (query.UserId.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.UserId == query.UserId.Value);
        }

        if (query.StartDate.HasValue)
        {
            var startUtc = JakartaTime.ToUtcStartOfDay(query.StartDate.Value);
            dbQuery = dbQuery.Where(a => a.TimestampUtc >= startUtc);
        }

        if (query.EndDate.HasValue)
        {
            var endUtc = JakartaTime.ToUtcEndOfDay(query.EndDate.Value);
            dbQuery = dbQuery.Where(a => a.TimestampUtc <= endUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(a =>
                (a.UserName != null && a.UserName.ToLower().Contains(search)) ||
                (a.EntityId != null && a.EntityId.ToLower().Contains(search)) ||
                (a.CorrelationId != null && a.CorrelationId.ToLower().Contains(search)) ||
                (a.Reason != null && a.Reason.ToLower().Contains(search)) ||
                a.EntityType.ToLower().Contains(search));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        // Sorting
        dbQuery = query.SortBy?.ToLowerInvariant() switch
        {
            "action" => query.SortDescending ? dbQuery.OrderByDescending(a => a.Action) : dbQuery.OrderBy(a => a.Action),
            "entitytype" => query.SortDescending ? dbQuery.OrderByDescending(a => a.EntityType) : dbQuery.OrderBy(a => a.EntityType),
            "username" => query.SortDescending ? dbQuery.OrderByDescending(a => a.UserName) : dbQuery.OrderBy(a => a.UserName),
            _ => query.SortDescending ? dbQuery.OrderBy(a => a.TimestampUtc) : dbQuery.OrderByDescending(a => a.TimestampUtc)
        };

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = a.UserName,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                TimestampUtc = a.TimestampUtc,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                AffectedColumns = a.AffectedColumns,
                Reason = a.Reason,
                CorrelationId = a.CorrelationId,
                IpAddress = a.IpAddress
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<AuditLogDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<AuditLogSummaryDto> GetAuditSummaryAsync(CancellationToken cancellationToken = default)
    {
        var total = await _dbContext.AuditLogs.CountAsync(cancellationToken);
        var creates = await _dbContext.AuditLogs.CountAsync(a => a.Action == "Create", cancellationToken);
        var updates = await _dbContext.AuditLogs.CountAsync(a => a.Action == "Update", cancellationToken);
        var deletes = await _dbContext.AuditLogs.CountAsync(a => a.Action == "Delete", cancellationToken);
        var postings = await _dbContext.AuditLogs.CountAsync(a => a.Action == "Post" || a.Action == "StockPosting", cancellationToken);
        var reversals = await _dbContext.AuditLogs.CountAsync(a => a.Action == "Reverse" || a.Action == "StockReversal", cancellationToken);
        var logins = await _dbContext.AuditLogs.CountAsync(a => a.Action.Contains("Login"), cancellationToken);

        var earliest = await _dbContext.AuditLogs
            .OrderBy(a => a.TimestampUtc)
            .Select(a => (DateTime?)a.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var latest = await _dbContext.AuditLogs
            .OrderByDescending(a => a.TimestampUtc)
            .Select(a => (DateTime?)a.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new AuditLogSummaryDto
        {
            TotalLogs = total,
            TotalCreates = creates,
            TotalUpdates = updates,
            TotalDeletes = deletes,
            TotalPostings = postings,
            TotalReversals = reversals,
            TotalLogins = logins,
            EarliestLogUtc = earliest,
            LatestLogUtc = latest
        };
    }

    public async Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.AuditLogs
            .Select(a => a.EntityType)
            .Where(e => !string.IsNullOrEmpty(e))
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetActionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.AuditLogs
            .Select(a => a.Action)
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(cancellationToken);
    }
}

