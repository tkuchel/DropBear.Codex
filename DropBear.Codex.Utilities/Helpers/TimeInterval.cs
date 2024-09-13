#region

using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

public static class TimeInterval
{
    // Obtain a Serilog logger instance for this class
    private static readonly ILogger Logger = LoggerFactory.Logger;

    /// <summary>
    ///     Converts seconds to milliseconds.
    /// </summary>
    /// <param name="seconds">The number of seconds to convert.</param>
    /// <returns>Milliseconds equivalent of the seconds provided.</returns>
    public static double FromSeconds(double seconds)
    {
        try
        {
            if (seconds < 0)
            {
                Logger.Warning("Attempt to convert negative seconds to milliseconds: {Seconds}", seconds);
                throw new ArgumentOutOfRangeException(nameof(seconds), "Seconds cannot be negative.");
            }

            var milliseconds = TimeSpan.FromSeconds(seconds).TotalMilliseconds;
            Logger.Information("Converted {Seconds} seconds to {Milliseconds} milliseconds.", seconds, milliseconds);
            return milliseconds;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while converting seconds to milliseconds.");
            throw;
        }
    }

    /// <summary>
    ///     Converts minutes to milliseconds.
    /// </summary>
    /// <param name="minutes">The number of minutes to convert.</param>
    /// <returns>Milliseconds equivalent of the minutes provided.</returns>
    public static double FromMinutes(double minutes)
    {
        try
        {
            if (minutes < 0)
            {
                Logger.Warning("Attempt to convert negative minutes to milliseconds: {Minutes}", minutes);
                throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes cannot be negative.");
            }

            var milliseconds = TimeSpan.FromMinutes(minutes).TotalMilliseconds;
            Logger.Information("Converted {Minutes} minutes to {Milliseconds} milliseconds.", minutes, milliseconds);
            return milliseconds;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while converting minutes to milliseconds.");
            throw;
        }
    }

    /// <summary>
    ///     Converts hours to milliseconds.
    /// </summary>
    /// <param name="hours">The number of hours to convert.</param>
    /// <returns>Milliseconds equivalent of the hours provided.</returns>
    public static double FromHours(double hours)
    {
        try
        {
            if (hours < 0)
            {
                Logger.Warning("Attempt to convert negative hours to milliseconds: {Hours}", hours);
                throw new ArgumentOutOfRangeException(nameof(hours), "Hours cannot be negative.");
            }

            var milliseconds = TimeSpan.FromHours(hours).TotalMilliseconds;
            Logger.Information("Converted {Hours} hours to {Milliseconds} milliseconds.", hours, milliseconds);
            return milliseconds;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while converting hours to milliseconds.");
            throw;
        }
    }

    /// <summary>
    ///     Converts days to milliseconds.
    /// </summary>
    /// <param name="days">The number of days to convert.</param>
    /// <returns>Milliseconds equivalent of the days provided.</returns>
    public static double FromDays(double days)
    {
        try
        {
            if (days < 0)
            {
                Logger.Warning("Attempt to convert negative days to milliseconds: {Days}", days);
                throw new ArgumentOutOfRangeException(nameof(days), "Days cannot be negative.");
            }

            var milliseconds = TimeSpan.FromDays(days).TotalMilliseconds;
            Logger.Information("Converted {Days} days to {Milliseconds} milliseconds.", days, milliseconds);
            return milliseconds;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while converting days to milliseconds.");
            throw;
        }
    }

    /// <summary>
    ///     Returns the given milliseconds as milliseconds (for completeness).
    /// </summary>
    /// <param name="milliseconds">The number of milliseconds.</param>
    /// <returns>The same value in milliseconds.</returns>
    public static double FromMilliseconds(double milliseconds)
    {
        try
        {
            if (milliseconds < 0)
            {
                Logger.Warning("Attempt to use negative milliseconds: {Milliseconds}", milliseconds);
                throw new ArgumentOutOfRangeException(nameof(milliseconds), "Milliseconds cannot be negative.");
            }

            Logger.Information("Returning {Milliseconds} milliseconds.", milliseconds);
            return milliseconds;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while handling milliseconds.");
            throw;
        }
    }
}
