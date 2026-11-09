namespace Wms.Domain.Common;

public interface IClock
{
    DateTime UtcNow { get; }
    DateTime TodayJakarta { get; }
}
