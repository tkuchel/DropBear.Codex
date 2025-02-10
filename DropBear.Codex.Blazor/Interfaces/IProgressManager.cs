using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Defines a thread-safe progress management interface for Blazor applications.
/// </summary>
public interface IProgressManager : IAsyncDisposable
{
    /// <summary>
    ///     Gets the current progress message.
    /// </summary>
    string Message { get; }

    /// <summary>
    ///     Gets the current progress value (0-100).
    ///     Thread-safe and atomic.
    /// </summary>
    double Progress { get; }

    /// <summary>
    ///     Gets whether the progress is in indeterminate mode.
    ///     Thread-safe and atomic.
    /// </summary>
    bool IsIndeterminate { get; }

    /// <summary>
    ///     Gets the step configurations, if any.
    ///     Thread-safe and immutable.
    /// </summary>
    IReadOnlyList<ProgressStepConfig>? Steps { get; }

    /// <summary>
    ///     Gets the current states of each step.
    ///     Thread-safe collection of immutable states.
    /// </summary>
    IReadOnlyList<StepState> CurrentStepStates { get; }

    /// <summary>
    ///     Gets the cancellation token for progress operations.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    ///     Gets whether this manager has been disposed.
    ///     Thread-safe and atomic.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    ///     Occurs when progress state changes.
    ///     Handlers should be async to support UI updates.
    /// </summary>
    event Func<Task>? StateChanged;

    /// <summary>
    ///     Starts an indeterminate progress indicator.
    /// </summary>
    /// <param name="message">Progress message.</param>
    /// <exception cref="ObjectDisposedException">If manager is disposed.</exception>
    void StartIndeterminate(string message);

    /// <summary>
    ///     Starts a determinate progress task.
    /// </summary>
    /// <param name="message">Progress message.</param>
    /// <exception cref="ObjectDisposedException">If manager is disposed.</exception>
    void StartTask(string message);

    /// <summary>
    ///     Starts a stepped progress indicator.
    /// </summary>
    /// <param name="steps">Step configurations.</param>
    /// <exception cref="ArgumentNullException">If steps is null.</exception>
    /// <exception cref="ArgumentException">If steps is empty.</exception>
    /// <exception cref="ObjectDisposedException">If manager is disposed.</exception>
    void StartSteps(List<ProgressStepConfig> steps);

    /// <summary>
    ///     Updates progress with thread-safe state management.
    /// </summary>
    /// <param name="taskId">Task/step identifier.</param>
    /// <param name="progress">Progress value (0-100).</param>
    /// <param name="status">Step status.</param>
    /// <param name="message">Optional message update.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <exception cref="ArgumentException">If taskId is invalid or progress out of range.</exception>
    /// <exception cref="ObjectDisposedException">If manager is disposed.</exception>
    /// <exception cref="OperationCanceledException">If operation is cancelled.</exception>
    Task UpdateProgressAsync(
        string taskId,
        double progress,
        StepStatus status,
        string? message = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks progress as complete.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If manager is disposed.</exception>
    void Complete();
}
