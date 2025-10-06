namespace DropBear.Codex.Core.Enums;

/// <summary>
///     Defines how the telemetry channel behaves when full.
/// </summary>
public enum ChannelFullMode
{
    /// <summary>
    ///     Wait for space to become available. May block the caller.
    /// </summary>
    Wait = 0,

    /// <summary>
    ///     Drop the oldest events to make room for new ones.
    ///     Recommended for most scenarios.
    /// </summary>
    DropOldest = 1,

    /// <summary>
    ///     Drop the newest events (current event being added).
    /// </summary>
    DropNewest = 2
}
