#region

using DropBear.Codex.Blazor.Components.Progress;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Blazor.Services;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using MessagePipe;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Interface for managing progress updates and state in a Blazor application.
///     Supports indeterminate, normal, and stepped progress modes, with optional integration
///     with the execution engine.
/// </summary>
public interface IExecutionProgressManager : IAsyncDisposable
{
    /// <summary>
    ///     Initializes the progress manager with a progress bar component.
    /// </summary>
    /// <param name="progressBar">The progress bar component to manage.</param>
    /// <returns>A result indicating success or failure of the initialization.</returns>
    Result<Unit, ProgressManagerError> Initialize(DropBearProgressBar progressBar);

    /// <summary>
    ///     Sets the progress bar to indeterminate mode for operations with unknown duration.
    /// </summary>
    /// <param name="message">Message to display during the indeterminate progress.</param>
    /// <returns>A result indicating success or failure of the mode change.</returns>
    Result<Unit, ProgressManagerError> SetIndeterminateMode(string message);

    /// <summary>
    ///     Sets the progress bar to normal mode for simple progress tracking.
    /// </summary>
    /// <returns>A result indicating success or failure of the mode change.</returns>
    Result<Unit, ProgressManagerError> SetNormalMode();

    /// <summary>
    ///     Sets the progress bar to stepped mode for tracking multiple sequential steps.
    /// </summary>
    /// <param name="steps">Configuration for the steps to track.</param>
    /// <returns>A result indicating success or failure of the mode change.</returns>
    Result<Unit, ProgressManagerError> SetSteppedMode(IReadOnlyList<ProgressStepConfig> steps);

    /// <summary>
    ///     Updates the current progress in normal mode.
    /// </summary>
    /// <param name="progress">Progress percentage (0-100).</param>
    /// <param name="message">Optional message to display.</param>
    /// <returns>A result indicating success or failure of the update.</returns>
    ValueTask<Result<Unit, ProgressManagerError>> UpdateProgressAsync(double progress, string? message = null);

    /// <summary>
    ///     Updates the progress of a specific step in stepped mode.
    /// </summary>
    /// <param name="stepId">Identifier of the step to update.</param>
    /// <param name="progress">Progress percentage for the step (0-100).</param>
    /// <param name="status">Current status of the step.</param>
    /// <returns>A result indicating success or failure of the update.</returns>
    ValueTask<Result<Unit, ProgressManagerError>> UpdateStepProgressAsync(string stepId, double progress,
        StepStatus status);

    /// <summary>
    ///     Completes the current progress operation.
    /// </summary>
    /// <returns>A result indicating success or failure of the completion.</returns>
    ValueTask<Result<Unit, ProgressManagerError>> CompleteAsync();

    /// <summary>
    ///     Enables integration with the execution engine for automatic progress updates.
    /// </summary>
    /// <returns>A result indicating success or failure of the integration setup.</returns>
    Result<Unit, ProgressManagerError> EnableExecutionEngineIntegration(
        Guid channelId,
        ISubscriber<Guid, TaskStartedMessage> taskStartedSubscriber,
        ISubscriber<Guid, TaskProgressMessage> taskProgressSubscriber,
        ISubscriber<Guid, TaskCompletedMessage> taskCompletedSubscriber,
        ISubscriber<Guid, TaskFailedMessage> taskFailedSubscriber);

    /// <summary>
    ///     Event raised when the progress manager's state changes
    /// </summary>
    event Action<ProgressManagerState>? OnStateChanged;
}
