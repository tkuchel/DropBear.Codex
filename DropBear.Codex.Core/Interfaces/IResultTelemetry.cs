#region

using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Provides telemetry information for result operations.
/// </summary>
public interface IResultTelemetry
{
    /// <summary>
    ///     Tracks when a result is created.
    /// </summary>
    void TrackResultCreated(ResultState state, Type resultType, string? caller = null);

    /// <summary>
    ///     Tracks when a result is transformed from one state to another.
    /// </summary>
    void TrackResultTransformed(ResultState originalState, ResultState newState, Type resultType,
        string? caller = null);

    /// <summary>
    ///     Tracks when an exception occurs during a result operation.
    /// </summary>
    void TrackException(Exception exception, ResultState state, Type resultType,
        string? caller = null);
}
