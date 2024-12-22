#region

using DropBear.Codex.Blazor.Services;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the current state of the progress manager
/// </summary>
public sealed record ProgressManagerState
{
    public ProgressManagerState(
        bool isIndeterminate,
        string message,
        double progress,
        IReadOnlyList<ProgressStepConfig>? steps,
        IReadOnlyDictionary<string, StepProgress>? stepStates = null)
    {
        IsIndeterminate = isIndeterminate;
        Message = message;
        Progress = progress;
        Steps = steps;
        StepStates = stepStates;
    }

    public bool IsIndeterminate { get; }
    public string Message { get; }
    public double Progress { get; }
    public IReadOnlyList<ProgressStepConfig>? Steps { get; }
    public IReadOnlyDictionary<string, StepProgress>? StepStates { get; }
}
