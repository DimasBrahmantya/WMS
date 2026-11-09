using Wms.Domain.Common;

namespace Wms.Infrastructure.Services;

public class DateTimeClock : IClock
{
    private static readonly TimeZoneInfo JakartaTimeZone = GetJakartaTimeZone();

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime TodayJakarta
    {
        get
        {
            var jakartaNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, JakartaTimeZone);
            return jakartaNow.Date;
        }
    }

    private static TimeZoneInfo GetJakartaTimeZone()
    {
        try
        {
            // Windows
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // Linux / IANA
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
