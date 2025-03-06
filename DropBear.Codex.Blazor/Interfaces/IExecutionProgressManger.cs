#region

using DropBear.Codex.Blazor.Components.Progress;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Blazor.Services;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.TaskExecutionEngine;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using MessagePipe;
using ProgressManagerError = DropBear.Codex.Blazor.Services.ProgressManagerError;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Defines a contract for bridging task execution progress (from ExecutionEngine) to a DropBearProgressBar.
/// </summary>
public interface IExecutionProgressManager : IAsyncDisposable
{
    /// <summary>
    ///     An event that fires whenever the manager updates progress (indeterminate, normal, or stepped).
    ///     Useful for other UI elements or logs that also need to react to changes.
    /// </summary>
    event Action<ProgressUpdate>? OnProgressUpdated;

    /// <summary>
    ///     Initializes the progress manager with a target <see cref="DropBearProgressBar" /> instance.
    ///     Must be called before other methods.
    /// </summary>
    /// <param name="progressBar">The Blazor progress bar component to update.</param>
    /// <returns>A result indicating success or failure.</returns>
    Result<Unit, ProgressManagerError> Initialize(DropBearProgressBar progressBar);

    /// <summary>
    ///     Sets the progress bar to indeterminate mode, typically used when the length of an operation is unknown.
    /// </summary>
    /// <param name="message">The message or label to display on the progress bar.</param>
    /// <returns>A result indicating success or failure.</returns>
    Result<Unit, ProgressManagerError> SetIndeterminateMode(string message);

    /// <summary>
    ///     Sets the progress bar to normal mode, allowing direct updates of progress percentage (0-100) without steps.
    /// </summary>
    /// <returns>A result indicating success or failure.</returns>
    Result<Unit, ProgressManagerError> SetNormalMode();

    /// <summary>
    ///     Sets the progress bar to stepped mode, showing multiple discrete steps in a pipeline or workflow.
    /// </summary>
    /// <param name="steps">A list of step configurations defining each step’s ID, name, tooltip, etc.</param>
    /// <returns>A result indicating success or failure.</returns>
    Result<Unit, ProgressManagerError> SetSteppedMode(IReadOnlyList<ProgressStepConfig> steps);

    /// <summary>
    ///     Updates the overall progress in normal mode.
    /// </summary>
    /// <param name="progress">The progress value (0-100).</param>
    /// <param name="message">Optional message to display.</param>
    /// <returns>A result indicating success or failure.</returns>
    ValueTask<Result<Unit, ProgressManagerError>> UpdateProgressAsync(double progress, string? message = null);

    /// <summary>
    ///     Updates a specific step’s progress and status in stepped mode.
    /// </summary>
    /// <param name="stepId">The unique step ID.</param>
    /// <param name="progress">The progress value (0-100).</param>
    /// <param name="status">The <see cref="StepStatus" /> to set for this step.</param>
    /// <returns>A result indicating success or failure.</returns>
    ValueTask<Result<Unit, ProgressManagerError>> UpdateStepProgressAsync(string stepId, double progress,
        StepStatus status);

    /// <summary>
    ///     Marks the entire process as complete.
    ///     Sets any unfinished steps to Completed (for stepped mode) or sets progress to 100% (for normal mode),
    ///     then hides the progress bar after a short delay.
    /// </summary>
    /// <returns>A result indicating success or failure.</returns>
    ValueTask<Result<Unit, ProgressManagerError>> CompleteAsync();


    /// <summary>
    ///     A flag indicating whether this manager has been disposed.
    /// </summary>
    /// <returns>A bool indicating if the manager has been disposed.</returns>
    bool IsDisposed { get; }

    /// <summary>
    ///     Subscribes to execution engine events (started, progress, completed, failed) using the given
    ///     <paramref name="channelId" />.
    ///     This wires the <see cref="ExecutionProgressManager" /> to the <see cref="ExecutionEngine" /> via MessagePipe.
    /// </summary>
    /// <param name="channelId">The channel ID used by the execution engine to publish messages.</param>
    /// <param name="taskStartedSubscriber">A subscriber for task-started messages.</param>
    /// <param name="taskProgressSubscriber">A subscriber for task-progress messages.</param>
    /// <param name="taskCompletedSubscriber">A subscriber for task-completed messages.</param>
    /// <param name="taskFailedSubscriber">A subscriber for task-failed messages.</param>
    /// <returns>A result indicating success or failure of enabling integration.</returns>
    Result<Unit, ProgressManagerError> EnableExecutionEngineIntegration(
        Guid channelId,
        IAsyncSubscriber<Guid, TaskStartedMessage> taskStartedSubscriber,
        IAsyncSubscriber<Guid, TaskProgressMessage> taskProgressSubscriber,
        IAsyncSubscriber<Guid, TaskCompletedMessage> taskCompletedSubscriber,
        IAsyncSubscriber<Guid, TaskFailedMessage> taskFailedSubscriber
    );
}
