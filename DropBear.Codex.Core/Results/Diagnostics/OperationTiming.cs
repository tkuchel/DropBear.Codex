using System.Runtime.InteropServices;

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Provides timing information for result operations.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct OperationTiming
{
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;

    public OperationTiming(DateTime startTime, DateTime? endTime = null)
    {
        StartTime = startTime;
        EndTime = endTime;
    }
}
