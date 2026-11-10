using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Domain.Entities.System;

namespace Wms.Infrastructure.Services.Stock;

public class DocumentSequenceService : IDocumentSequenceService
{
    private readonly IWmsDbContext _dbContext;
    private static readonly SemaphoreSlim _inMemoryLock = new(1, 1);

    public DocumentSequenceService(IWmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GetNextDocumentNumberAsync(
        string prefix,
        DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        var targetDate = date ?? DateTime.UtcNow;
        var period = targetDate.ToString("yyyyMM");

        await _inMemoryLock.WaitAsync(cancellationToken);
        try
        {
            var sequence = await _dbContext.DocumentSequences
                .FirstOrDefaultAsync(s => s.Prefix == prefix && s.Period == period, cancellationToken);

            if (sequence == null)
            {
                sequence = new DocumentSequence
                {
                    Id = Guid.NewGuid(),
                    Prefix = prefix,
                    Period = period,
                    LastNumber = 1,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _dbContext.DocumentSequences.Add(sequence);
            }
            else
            {
                sequence.LastNumber += 1;
                sequence.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return $"{prefix}-{period}-{sequence.LastNumber:D6}";
        }
        finally
        {
            _inMemoryLock.Release();
        }
    }
}

