namespace DropBear.Codex.Core.Enums;

/// <summary>
///     Defines the telemetry collection mode for result operations.
///     Optimized for .NET 9 with configurable overhead.
/// </summary>
public enum TelemetryMode
{
    /// <summary>
    ///     Telemetry is completely disabled. Zero overhead.
    ///     Use this in production when telemetry is not needed.
    /// </summary>
    Disabled = 0,

    /// <summary>
    ///     Telemetry events are tracked asynchronously using fire-and-forget Task.Run.
    ///     Provides low latency but may saturate thread pool under extreme load.
    ///     This is the default mode for backward compatibility.
    /// </summary>
    FireAndForget = 1,

    /// <summary>
    ///     Telemetry events are queued to a bounded channel and processed by a background service.
    ///     Provides better backpressure handling and prevents thread pool saturation.
    ///     Recommended for high-throughput production scenarios.
    /// </summary>
    BackgroundChannel = 2,

    /// <summary>
    ///     Telemetry events are processed synchronously on the calling thread.
    ///     Use only for testing and debugging - adds significant overhead.
    /// </summary>
    Synchronous = 3
}
