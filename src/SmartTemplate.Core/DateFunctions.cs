namespace SmartTemplate.Core;

/// <summary>
/// Custom date helper methods exposed as Scriban functions under the "date" built-in object.
/// Scriban 6.x already provides date.now, date.to_string, date.parse, date.add_days,
/// date.add_months, date.add_years.  We add date.today and date.format as extras.
/// These static methods are also testable independently of Scriban.
/// </summary>
public static class DateFunctions
{
    /// <summary>Parses a date from an ISO string or "today"/"now" keywords.</summary>
    public static DateTime Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Cannot parse empty date string.");

        return value.ToLowerInvariant() switch
        {
            "today" => DateTime.Today,
            "now"   => DateTime.Now,
            _ => DateTime.Parse(value)
        };
    }

    /// <summary>Alias for date.to_string — formats a DateTime using a strftime-style format string.</summary>
    public static string Format(DateTime date, string format) =>
        date.ToString(
            format.Replace("%Y", "yyyy").Replace("%m", "MM").Replace("%d", "dd")
                  .Replace("%H", "HH").Replace("%M", "mm").Replace("%S", "ss"));

    /// <summary>Adds the specified number of days to a date.</summary>
    public static DateTime AddDays(DateTime date, int days) => date.AddDays(days);

    /// <summary>Adds the specified number of months to a date.</summary>
    public static DateTime AddMonths(DateTime date, int months) => date.AddMonths(months);

    /// <summary>Adds the specified number of years to a date.</summary>
    public static DateTime AddYears(DateTime date, int years) => date.AddYears(years);
}
