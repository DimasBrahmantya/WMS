using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Stock.Idempotency;
using Wms.Domain.Entities.System;

namespace Wms.Infrastructure.Services.Stock;

public class IdempotencyService : IIdempotencyService
{
    private readonly IWmsDbContext _dbContext;

    public IdempotencyService(IWmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Guid userId,
        string operation,
        string key,
        Func<Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return await action();
        }

        // Check if an existing idempotency record exists
        var existing = await _dbContext.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.UserId == userId && k.Operation == operation && k.Key == key, cancellationToken);

        if (existing != null && !string.IsNullOrEmpty(existing.ResultJson))
        {
            var cached = JsonSerializer.Deserialize<TResult>(existing.ResultJson);
            if (cached != null)
            {
                return cached;
            }
        }

        // Execute action
        var result = await action();

        // Save idempotency record
        var resultJson = JsonSerializer.Serialize(result);
        var record = new IdempotencyKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Operation = operation,
            Key = key,
            ResultJson = resultJson,
            StatusCode = 200,
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            _dbContext.IdempotencyKeys.Add(record);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Concurrent submission: fetch the one that was saved
            var saved = await _dbContext.IdempotencyKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.UserId == userId && k.Operation == operation && k.Key == key, cancellationToken);

            if (saved != null && !string.IsNullOrEmpty(saved.ResultJson))
            {
                var cached = JsonSerializer.Deserialize<TResult>(saved.ResultJson);
                if (cached != null)
                {
                    return cached;
                }
            }
        }

        return result;
    }
}

