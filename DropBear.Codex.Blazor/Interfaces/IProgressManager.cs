#region

using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Defines a contract for managing progress updates in Blazor applications.
/// </summary>
public interface IProgressManager : IDisposable
{
    /// <summary>
    ///     Gets the current progress message.
    /// </summary>
    string Message { get; }

    /// <summary>
    ///     Gets the overall progress percentage.
    /// </summary>
    double Progress { get; }

    /// <summary>
    ///     Gets a value indicating whether the progress is indeterminate.
    /// </summary>
    bool IsIndeterminate { get; }

    /// <summary>
    ///     Gets the step configurations, if any.
    /// </summary>
    IReadOnlyList<ProgressStepConfig>? Steps { get; }

    /// <summary>
    ///     Gets the cancellation token for progress operations.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    ///     Occurs when the progress state changes.
    /// </summary>
    event Action StateChanged;

    /// <summary>
    ///     Starts an indeterminate progress operation.
    /// </summary>
    /// <param name="message">Message to display during the operation.</param>
    void StartIndeterminate(string message);

    /// <summary>
    ///     Starts tracking a single task's progress.
    /// </summary>
    /// <param name="message">Message to display during the operation.</param>
    void StartTask(string message);

    /// <summary>
    ///     Starts a step-based progress operation.
    /// </summary>
    /// <param name="steps">Configurations for the steps.</param>
    void StartSteps(List<ProgressStepConfig> steps);

    /// <summary>
    ///     Updates progress for a specific task or step.
    /// </summary>
    /// <param name="taskId">The ID of the task or step.</param>
    /// <param name="progress">The progress percentage (0-100).</param>
    /// <param name="message">An optional progress message.</param>
    Task UpdateProgressAsync(string taskId, double progress, string? message = null);

    /// <summary>
    ///     Marks the current progress operation as complete.
    /// </summary>
    void Complete();
}
