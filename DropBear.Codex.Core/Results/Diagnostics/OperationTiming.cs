#region

using System.Runtime.InteropServices;

#endregion

namespace DropBear.Codex.Core.Results.Diagnostics;

/// <summary>
///     Provides timing information for result operations.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct OperationTiming
{
    /// <summary>
    ///     The UTC time when the operation started.
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    ///     The UTC time when the operation ended, or null if still in progress.
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    ///     The duration of the operation. If the operation is still in progress, this is the time elapsed since StartTime.
    /// </summary>
    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OperationTiming" /> struct.
    /// </summary>
    /// <param name="startTime">The start time of the operation.</param>
    /// <param name="endTime">The end time of the operation, or null if still in progress. </param>
    public OperationTiming(DateTime startTime, DateTime? endTime = null)
    {
        StartTime = startTime;
        EndTime = endTime;
    }
}
