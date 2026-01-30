namespace Pocketree.Shared.Extensions;

/// <summary>
/// Extension methods for DateTime operations
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Checks if a date is today (UTC)
    /// </summary>
    public static bool IsToday(this DateTime date)
    {
        return date.Date == DateTime.UtcNow.Date;
    }

    /// <summary>
    /// Checks if a date was yesterday (UTC)
    /// </summary>
    public static bool IsYesterday(this DateTime date)
    {
        return date.Date == DateTime.UtcNow.Date.AddDays(-1);
    }

    /// <summary>
    /// Gets the start of the day (00:00:00)
    /// </summary>
    public static DateTime StartOfDay(this DateTime date)
    {
        return date.Date;
    }

    /// <summary>
    /// Gets the end of the day (23:59:59.999)
    /// </summary>
    public static DateTime EndOfDay(this DateTime date)
    {
        return date.Date.AddDays(1).AddTicks(-1);
    }

    /// <summary>
    /// Calculates days since a given date
    /// </summary>
    public static int DaysSince(this DateTime date)
    {
        return (DateTime.UtcNow.Date - date.Date).Days;
    }

    /// <summary>
    /// Checks if the date is within the last N days
    /// </summary>
    public static bool IsWithinLastDays(this DateTime date, int days)
    {
        return date >= DateTime.UtcNow.AddDays(-days);
    }
}
