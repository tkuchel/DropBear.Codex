#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

public interface IProgressManager : IDisposable
{
    string Message { get; }
    double Progress { get; }
    bool IsIndeterminate { get; }
    IReadOnlyList<ProgressStepConfig>? Steps { get; }
    IReadOnlyList<StepState> CurrentStepStates { get; }

    CancellationToken CancellationToken { get; }

    bool IsDisposed { get; }

    event Action? StateChanged;

    void StartIndeterminate(string message);
    void StartTask(string message);
    void StartSteps(List<ProgressStepConfig> steps);
    Task UpdateProgressAsync(string taskId, double progress, StepStatus status, string? message = null);
    void Complete();
}
