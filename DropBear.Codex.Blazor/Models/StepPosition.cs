namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the position of a step relative to the current step.
/// </summary>
public enum StepPosition
{
    /// <summary>
    ///     Step has been completed (is before the current step).
    /// </summary>
    Previous,

    /// <summary>
    ///     Step is currently active.
    /// </summary>
    Current,

    /// <summary>
    ///     Step is upcoming (after the current step).
    /// </summary>
    Next
}
