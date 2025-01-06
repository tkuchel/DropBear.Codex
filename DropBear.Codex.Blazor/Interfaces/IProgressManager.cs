#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Interface for managing progress information in a Blazor application.
/// </summary>
public interface IProgressManager : IDisposable
{
    /// <summary>
    ///     Gets the current progress message.
    /// </summary>
    string Message { get; }

    /// <summary>
    ///     Gets the current progress value (0-100).
    ///     Ignored if <see cref="IsIndeterminate" /> is <c>true</c>.
    /// </summary>
    double Progress { get; }

    /// <summary>
    ///     Gets a value indicating whether this progress manager is running in an indeterminate mode.
    /// </summary>
    bool IsIndeterminate { get; }

    /// <summary>
    ///     Gets the step configurations, if any.
    /// </summary>
    IReadOnlyList<ProgressStepConfig>? Steps { get; }

    /// <summary>
    ///     Gets the current states of each step when using stepped progress.
    /// </summary>
    IReadOnlyList<StepState> CurrentStepStates { get; }

    /// <summary>
    ///     Gets the <see cref="System.Threading.CancellationToken" /> used to signal cancellation of this progress.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    ///     Gets a value indicating whether this progress manager has been disposed.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    ///     Occurs when any progress state changes.
    ///     Subscribers should re-render or update UI elements as needed.
    /// </summary>
    event Action? StateChanged;

    /// <summary>
    ///     Starts an indeterminate progress indicator with the specified message.
    /// </summary>
    /// <param name="message">Message describing the current progress or activity.</param>
    void StartIndeterminate(string message);

    /// <summary>
    ///     Starts a task with a determinate progress bar and the specified message.
    /// </summary>
    /// <param name="message">Message describing the current progress or activity.</param>
    void StartTask(string message);

    /// <summary>
    ///     Starts a stepped progress indicator with the specified step configuration.
    /// </summary>
    /// <param name="steps">List of step configurations.</param>
    void StartSteps(List<ProgressStepConfig> steps);

    /// <summary>
    ///     Updates the progress of an ongoing task, optionally changing its status and message.
    /// </summary>
    /// <param name="taskId">Identifier for the task/step to update.</param>
    /// <param name="progress">
    ///     A value between 0 and 100 indicating completion percentage
    ///     (or a custom scale if your UI interprets it differently).
    /// </param>
    /// <param name="status">The new status of the task.</param>
    /// <param name="message">An optional updated message.</param>
    /// <returns>An awaitable task to allow asynchronous operations within this method.</returns>
    Task UpdateProgressAsync(string taskId, double progress, StepStatus status, string? message = null);

    /// <summary>
    ///     Marks the progress as complete, typically hiding or resetting any progress UI.
    /// </summary>
    void Complete();
}
