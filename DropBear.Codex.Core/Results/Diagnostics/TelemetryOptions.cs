#region

using System.ComponentModel.DataAnnotations;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Configuration options for result telemetry.
///     Optimized for .NET 9 with validation and sensible defaults.
/// </summary>
public sealed class TelemetryOptions
{
    /// <summary>
    ///     Gets or sets the telemetry collection mode.
    ///     Default: FireAndForget for backward compatibility.
    /// </summary>
    public TelemetryMode Mode { get; set; } = TelemetryMode.FireAndForget;

    /// <summary>
    ///     Gets or sets the capacity of the telemetry event channel when using BackgroundChannel mode.
    ///     Default: 10,000 events. Must be between 100 and 1,000,000.
    /// </summary>
    [Range(100, 1_000_000)]
    public int ChannelCapacity { get; set; } = 10_000;

    /// <summary>
    ///     Gets or sets whether to collect in-memory statistics.
    ///     Disable for maximum performance in production.
    ///     Default: false.
    /// </summary>
    public bool CollectStatistics { get; set; }

    /// <summary>
    ///     Gets or sets whether to include stack traces in exception telemetry.
    ///     Disable for better performance, enable for detailed diagnostics.
    ///     Default: true in Debug, false in Release.
    /// </summary>
#if DEBUG
    public bool CaptureStackTraces { get; set; } = true;
#else
    public bool CaptureStackTraces { get; set; }
#endif

    /// <summary>
    ///     Gets or sets the maximum number of events to buffer before dropping old events
    ///     when the channel is full (BackgroundChannel mode only).
    ///     Default: DropOldest behavior.
    /// </summary>
    public ChannelFullMode FullMode { get; set; } = ChannelFullMode.DropOldest;

    /// <summary>
    ///     Gets or sets the cancellation token source for the background processor.
    ///     If not set, a default one will be created.
    /// </summary>
    public CancellationTokenSource? CancellationTokenSource { get; set; }

    /// <summary>
    ///     Validates the options and returns any validation errors.
    ///     Uses collection expressions for modern syntax.
    /// </summary>
    /// <returns>A collection of validation error messages, or empty if valid.</returns>
    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (ChannelCapacity is < 100 or > 1_000_000)
        {
            errors.Add($"ChannelCapacity must be between 100 and 1,000,000. Current value: {ChannelCapacity}");
        }

        if (Mode == TelemetryMode.BackgroundChannel && ChannelCapacity < 1000)
        {
            errors.Add("ChannelCapacity should be at least 1,000 for BackgroundChannel mode to be effective.");
        }

        return errors;
    }

    /// <summary>
    ///     Creates options optimized for development with full diagnostics.
    /// </summary>
    public static TelemetryOptions Development() => new()
    {
        Mode = TelemetryMode.Synchronous,
        CollectStatistics = true,
        CaptureStackTraces = true,
        ChannelCapacity = 1_000
    };

    /// <summary>
    ///     Creates options optimized for production with minimal overhead.
    /// </summary>
    public static TelemetryOptions Production() => new()
    {
        Mode = TelemetryMode.BackgroundChannel,
        CollectStatistics = false,
        CaptureStackTraces = false,
        ChannelCapacity = 50_000
    };

    /// <summary>
    ///     Creates options with telemetry completely disabled.
    /// </summary>
    public static TelemetryOptions Disabled() => new()
    {
        Mode = TelemetryMode.Disabled, CollectStatistics = false, CaptureStackTraces = false
    };
}
