#region

using System.Collections.Frozen;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for time conversions, optimized for .NET 8 features.
/// </summary>
public static class TimeInterval
{
    private static readonly FrozenDictionary<string, Func<double, double>> TimeConverters =
        new Dictionary<string, Func<double, double>>
            (StringComparer.Ordinal)
            {
                ["seconds"] = seconds => TimeSpan.FromSeconds(seconds).TotalMilliseconds,
                ["minutes"] = minutes => TimeSpan.FromMinutes(minutes).TotalMilliseconds,
                ["hours"] = hours => TimeSpan.FromHours(hours).TotalMilliseconds,
                ["days"] = days => TimeSpan.FromDays(days).TotalMilliseconds
            }.ToFrozenDictionary();


    /// <summary>
    ///     Converts a given time value to milliseconds.
    ///     Uses <see cref="FrozenDictionary" /> for fast lookups.
    /// </summary>
    public static Result<double, TimeError> ConvertToMilliseconds(double value, string unit)
    {
        if (value < 0)
        {
            return Result<double, TimeError>.Failure(new TimeError("Time value cannot be negative."));
        }

        try
        {
            if (!TimeConverters.TryGetValue(unit.ToLowerInvariant(), out var converter))
            {
                return Result<double, TimeError>.Failure(new TimeError("Invalid time unit."));
            }

            return Result<double, TimeError>.Success(converter(value));
        }
        catch (Exception ex)
        {
            return Result<double, TimeError>.Failure(new TimeError("Failed to convert time to milliseconds.", ex));
        }
    }
}
