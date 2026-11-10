using System.Globalization;

namespace Wms.Domain.Common;

/// <summary>
/// Utility untuk konversi waktu zona Asia/Jakarta (WIB, UTC+7)
/// sesuai bagian 29 spesifikasi: simpan UTC di database, tampilkan Asia/Jakarta, locale id-ID.
/// </summary>
public static class JakartaTime
{
    private static readonly CultureInfo IndonesianCulture = new("id-ID");
    public static readonly TimeZoneInfo TimeZone = GetJakartaTimeZone();

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
                // Linux / macOS / IANA
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
            }
            catch
            {
                // Fallback UTC+7
                return TimeZoneInfo.CreateCustomTimeZone("WIB", TimeSpan.FromHours(7), "WIB", "WIB");
            }
        }
    }

    /// <summary>
    /// Mengambil waktu saat ini dalam zona Jakarta (WIB).
    /// </summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

    /// <summary>
    /// Mengambil tanggal hari ini dalam zona Jakarta (jam 00:00:00).
    /// </summary>
    public static DateTime Today => Now.Date;

    /// <summary>
    /// Mengubah waktu UTC menjadi waktu Jakarta (WIB).
    /// </summary>
    public static DateTime ToJakarta(DateTime utcDateTime)
    {
        var utc = utcDateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)
            : utcDateTime.ToUniversalTime();

        return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZone);
    }

    /// <summary>
    /// Mengubah tanggal lokal Jakarta (WIB) ke awal hari UTC (00:00:00.000000).
    /// </summary>
    public static DateTime ToUtcStartOfDay(DateTime jakartaDate)
    {
        var local = new DateTime(jakartaDate.Year, jakartaDate.Month, jakartaDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, TimeZone);
    }

    /// <summary>
    /// Mengubah tanggal lokal Jakarta (WIB) ke akhir hari UTC (23:59:59.999999).
    /// </summary>
    public static DateTime ToUtcEndOfDay(DateTime jakartaDate)
    {
        var local = new DateTime(jakartaDate.Year, jakartaDate.Month, jakartaDate.Day, 23, 59, 59, 999, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, TimeZone);
    }

    /// <summary>
    /// Format tanggal/waktu ke string bahasa Indonesia dengan penanda zona WIB.
    /// Contoh: "10 November 2026 19:30 WIB"
    /// </summary>
    public static string Format(DateTime utcDateTime, string format = "dd MMMM yyyy HH:mm")
    {
        var jakarta = ToJakarta(utcDateTime);
        return $"{jakarta.ToString(format, IndonesianCulture)} WIB";
    }

    /// <summary>
    /// Format tanggal saja ke string bahasa Indonesia.
    /// Contoh: "10 November 2026"
    /// </summary>
    public static string FormatDate(DateTime utcDateTime, string format = "dd MMMM yyyy")
    {
        var jakarta = ToJakarta(utcDateTime);
        return jakarta.ToString(format, IndonesianCulture);
    }
}

