using DropBear.Codex.Core.Enums;

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Represents a telemetry event to be processed.
/// </summary>
internal readonly record struct TelemetryEvent
{
    public required TelemetryEventType Type { get; init; }
    public required ResultState State { get; init; }
    public ResultState? OriginalState { get; init; }
    public required Type ResultType { get; init; }
    public Exception? Exception { get; init; }
    public string? Caller { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
