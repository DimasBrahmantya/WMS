namespace Wms.Application.Features.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(DashboardFilterDto filter, CancellationToken cancellationToken = default);
}

