using Quartz;
using Wms.Application.Features.Exports;

namespace Wms.Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class CleanupExportFilesJob : IJob
{
    private readonly IExportService _exportService;

    public CleanupExportFilesJob(IExportService exportService)
    {
        _exportService = exportService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await _exportService.CleanupExpiredExportsAsync(context.CancellationToken);
    }
}

