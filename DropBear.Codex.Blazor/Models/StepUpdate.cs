using DropBear.Codex.Blazor.Enums;

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents an update to a specific progress step.
/// </summary>
/// <param name="StepId">The unique identifier of the step.</param>
/// <param name="Progress">The progress value (0-100).</param>
/// <param name="Status">The step status.</param>
public sealed record StepUpdate(string StepId, double Progress, StepStatus Status)
{
    /// <summary>
    ///     Gets the timestamp when this update was created.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}
