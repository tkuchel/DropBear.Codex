#region

using System.Collections.Frozen;
using System.Globalization;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for date manipulation and formatting, optimized for .NET 8.
/// </summary>
public static class DateHelper
{
    private static readonly FrozenDictionary<string, string> DateFormats = new Dictionary<string, string>
        (StringComparer.Ordinal)
        {
            { "StandardDate", "dd/MM/yyyy" },
            { "StandardDateTime", "dd/MM/yyyy HH:mm:ss" },
            { "ISO8601", "yyyy-MM-ddTHH:mm:ss.fffZ" }
        }.ToFrozenDictionary();

    /// <summary>
    ///     Formats a <see cref="DateTime" /> using predefined formats.
    /// </summary>
    public static Result<string, DateError> ToFormattedString(this DateTime dateTime,
        string formatKey = "StandardDateTime")
    {
        if (!DateFormats.TryGetValue(formatKey, out var format))
        {
            return Result<string, DateError>.Failure(new DateError("Invalid format key."));
        }

        try
        {
            return Result<string, DateError>.Success(dateTime.ToString(format, CultureInfo.CurrentCulture));
        }
        catch (Exception ex)
        {
            return Result<string, DateError>.Failure(new DateError("Failed to format DateTime.", ex));
        }
    }

    /// <summary>
    ///     Parses a date string into a DateTime object using multiple formats.
    ///     Uses <see cref="Span{T}" /> for optimized string parsing.
    /// </summary>
    public static Result<DateTime, DateError> ParseDate(ReadOnlySpan<char> dateString)
    {
        foreach (var format in DateFormats.Values)
        {
            if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var parsedDate))
            {
                return Result<DateTime, DateError>.Success(parsedDate);
            }
        }

        return Result<DateTime, DateError>.Failure(new DateError("Failed to parse date."));
    }

    /// <summary>
    ///     Converts a <see cref="DateTime" /> to Unix time.
    /// </summary>
    public static Result<long, DateError> ToUnixTime(this DateTime dateTime)
    {
        try
        {
            return Result<long, DateError>.Success((long)(dateTime.ToUniversalTime() - DateTime.UnixEpoch)
                .TotalSeconds);
        }
        catch (Exception ex)
        {
            return Result<long, DateError>.Failure(new DateError("Failed to convert to Unix time.", ex));
        }
    }
}
