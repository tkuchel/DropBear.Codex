#region

using System.Globalization;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for date manipulation and formatting.
/// </summary>
public static class DateHelper
{
    /// <summary>
    ///     Standard date format used across the application.
    /// </summary>
    private const string StandardDateFormat = "dd/MM/yyyy";

    /// <summary>
    ///     Standard time format used across the application.
    /// </summary>
    public const string StandardTimeFormat = "HH:mm:ss";

    /// <summary>
    ///     Standard datetime format used across the application.
    /// </summary>
    private const string StandardDateTimeFormat = "dd/MM/yyyy HH:mm:ss";

    /// <summary>
    ///     Formats a <see cref="DateTime" /> according to whether it includes a time part.
    /// </summary>
    /// <param name="dateTime">The <see cref="DateTime" /> to format.</param>
    /// <param name="dateFormat">Format string for dates without time.</param>
    /// <param name="dateTimeFormat">Format string for dates with time.</param>
    /// <returns>Formatted date string.</returns>
    public static string ToDisplayString(this DateTime dateTime,
        string dateFormat = StandardDateFormat,
        string dateTimeFormat = StandardDateTimeFormat)
    {
        var isDateOnly = dateTime.TimeOfDay == TimeSpan.Zero;
        return dateTime.ToString(isDateOnly ? dateFormat : dateTimeFormat, CultureInfo.CurrentCulture);
    }

    /// <summary>
    ///     Calculates age in years from a given birth date to today's date.
    /// </summary>
    /// <param name="dateOfBirth">Birth date.</param>
    /// <param name="today">Today's date.</param>
    /// <returns>Age in years, or null if birth date is invalid.</returns>
    public static int? CalculateAgeInYears(DateTime? dateOfBirth, DateTime today)
    {
        if (!dateOfBirth.HasValue || dateOfBirth.Value > today)
        {
            return null;
        }

        var age = today.Year - dateOfBirth.Value.Year;

        if (today < dateOfBirth.Value.AddYears(age))
        {
            age--;
        }

        return age;
    }

    /// <summary>
    ///     Calculates the number of years between two dates.
    /// </summary>
    /// <param name="fromDate">Start date.</param>
    /// <param name="toDate">End date.</param>
    /// <returns>Number of years between the dates.</returns>
    public static int YearsApart(DateTime fromDate, DateTime toDate)
    {
        var years = toDate.Year - fromDate.Year;

        if (toDate < fromDate.AddYears(years))
        {
            years--;
        }

        return years;
    }

    /// <summary>
    ///     Gets the start of the day for a given <see cref="DateTime" />.
    /// </summary>
    /// <param name="dateTime">The date.</param>
    /// <returns>Start of the day (00:00:00).</returns>
    public static DateTime StartOfDay(this DateTime dateTime)
    {
        return dateTime.Date;
    }

    /// <summary>
    ///     Gets the end of the day for a given <see cref="DateTime" />.
    /// </summary>
    /// <param name="dateTime">The date.</param>
    /// <returns>End of the day (23:59:59).</returns>
    public static DateTime EndOfDay(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 23, 59, 59);
    }

    /// <summary>
    ///     Checks if a nullable <see cref="DateTime" /> is between two dates, inclusive.
    /// </summary>
    /// <param name="dateTime">The date to check.</param>
    /// <param name="lowerBound">Lower bound date.</param>
    /// <param name="upperBound">Upper bound date.</param>
    /// <returns>True if the date is within the bounds; otherwise, false.</returns>
    public static bool IsBetweenInclusive(this DateTime? dateTime, DateTime lowerBound, DateTime upperBound)
    {
        return dateTime.HasValue && dateTime.Value >= lowerBound && dateTime.Value <= upperBound;
    }

    /// <summary>
    ///     Converts a <see cref="DateTime" /> to Unix time.
    /// </summary>
    /// <param name="dateTime">The date to convert.</param>
    /// <returns>Unix time representation of the date.</returns>
    public static long ToUnixTime(this DateTime dateTime)
    {
        return (long)(dateTime.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;
    }

    /// <summary>
    ///     Determines whether the given date of birth indicates that the person is under 18 years of age.
    /// </summary>
    /// <param name="dateOfBirth">The date of birth of the person.</param>
    /// <returns>
    ///     <c>true</c> if the person is under 18 years of age based on the provided date of birth; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsUnder18(DateTimeOffset? dateOfBirth)
    {
        return IsUnderAgeLimit(dateOfBirth, 18);
    }

    /// <summary>
    ///     Determines whether the given date of birth indicates that the person is under the specified age limit.
    /// </summary>
    /// <param name="dateOfBirth">The date of birth of the person.</param>
    /// <param name="ageLimit">The age limit to check against.</param>
    /// <returns>
    ///     <c>true</c> if the person is under the specified age limit based on the provided date of birth; otherwise,
    ///     <c>false</c>.
    /// </returns>
    public static bool IsUnderAgeLimit(DateTimeOffset? dateOfBirth, int ageLimit)
    {
        if (dateOfBirth == null || ageLimit < 0)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        return dateOfBirth > now.AddYears(-ageLimit);
    }
}
