using Microsoft.EntityFrameworkCore;
using Quartz;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Exports;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class BackgroundExportWorkerJob : IJob
{
    private readonly IWmsDbContext _context;
    private readonly IExportService _exportService;

    public BackgroundExportWorkerJob(IWmsDbContext context, IExportService exportService)
    {
        _context = context;
        _exportService = exportService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        var pendingJobIds = await _context.ExportJobs
            .Where(j => j.Status == ExportJobStatus.Pending)
            .OrderBy(j => j.CreatedAtUtc)
            .Take(5)
            .Select(j => j.Id)
            .ToListAsync(cancellationToken);

        foreach (var jobId in pendingJobIds)
        {
            await _exportService.ProcessExportJobAsync(jobId, cancellationToken);
        }
    }
}
