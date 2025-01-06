#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Components.Progress;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.TaskExecutionEngine;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Bridges the <see cref="ExecutionEngine" /> messages (via MessagePipe) to the <see cref="DropBearProgressBar" />.
///     Handles both indeterminate, normal, and stepped progress modes without directly writing to the bar's parameters.
/// </summary>
public sealed class ExecutionProgressManager : IExecutionProgressManager
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Concurrent dictionary holding step states for stepped mode.
    ///     Thread-safe for multiple updates from different tasks.
    /// </summary>
    private readonly ConcurrentDictionary<string, StepUpdate> _stepStates = new();

    /// <summary>
    ///     A semaphore lock to protect critical sections in async update methods.
    /// </summary>
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    // Holds disposable resources from MessagePipe subscriptions.
    private DisposableBagBuilder? _bagBuilder;
    private string _currentMessage = string.Empty;
    private double _currentProgress;
    private IDisposable? _disposableBag;
    private bool _isIndeterminateMode;
    private bool _isSteppedMode;
    private bool _isVisible;

    // Current state flags & data
    private DropBearProgressBar? _progressBar;
    private IReadOnlyList<ProgressStepConfig>? _steps;

    /// <summary>
    ///     Creates a new <see cref="ExecutionProgressManager" /> instance.
    /// </summary>
    public ExecutionProgressManager()
    {
        _logger = LoggerFactory.Logger.ForContext<ExecutionProgressManager>();
    }

    /// <summary>
    ///     Indicates whether this manager has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    ///     Event fired whenever the manager updates progress, providing a <see cref="ProgressUpdate" />.
    ///     Allows external observers to react (e.g., re-render) on progress changes.
    /// </summary>
    public event Action<ProgressUpdate>? OnProgressUpdated;

    /// <summary>
    ///     Initializes the manager with a target <see cref="DropBearProgressBar" /> instance.
    ///     Must be called before other methods are used.
    /// </summary>
    /// <param name="progressBar">The progress bar component to control.</param>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    public Result<Unit, ProgressManagerError> Initialize(DropBearProgressBar progressBar)
    {
        ThrowIfDisposed();

        try
        {
            _progressBar = progressBar ?? throw new ArgumentNullException(nameof(progressBar));
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize progress manager");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to initialize progress manager", ex));
        }
    }

    /// <summary>
    ///     Sets the progress bar to indeterminate mode (e.g., spinner).
    /// </summary>
    /// <param name="message">Message displayed to the user.</param>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    public Result<Unit, ProgressManagerError> SetIndeterminateMode(string message)
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        try
        {
            _isIndeterminateMode = true;
            _isSteppedMode = false;
            _currentMessage = message;
            _currentProgress = 0;
            _isVisible = true;
            _steps = null;

            // Asynchronously set the progress bar to indeterminate mode
            _ = _progressBar!.SetIndeterminateModeAsync(message);

            NotifyProgressUpdate();
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set indeterminate mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set indeterminate mode", ex));
        }
    }

    /// <summary>
    ///     Sets the progress bar to normal (determinate) mode without discrete steps.
    /// </summary>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    public Result<Unit, ProgressManagerError> SetNormalMode()
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        try
        {
            _isIndeterminateMode = false;
            _isSteppedMode = false;
            _currentMessage = "Starting...";
            _currentProgress = 0;
            _isVisible = true;
            _steps = null;

            // Asynchronously set the bar to normal mode
            _ = _progressBar!.SetNormalProgressAsync(0, _currentMessage);

            NotifyProgressUpdate();
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set normal mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set normal mode", ex));
        }
    }

    /// <summary>
    ///     Sets the progress bar to stepped mode with a provided list of step configurations.
    /// </summary>
    /// <param name="steps">A read-only list of step configurations.</param>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    public Result<Unit, ProgressManagerError> SetSteppedMode(IReadOnlyList<ProgressStepConfig> steps)
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        try
        {
            _isIndeterminateMode = false;
            _isSteppedMode = true;
            _isVisible = true;
            _currentMessage = steps.Count > 0 ? $"Step 1 of {steps.Count}" : "No steps";
            _currentProgress = 0;
            _steps = steps;

            // Clear any existing step states and create new ones
            _stepStates.Clear();
            foreach (var step in steps)
            {
                var stepUpdate = new StepUpdate(step.Id, 0, StepStatus.NotStarted);
                _stepStates[step.Id] = stepUpdate;
            }

            // Initialize the bar in normal mode (0%) and set steps
            _ = _progressBar!.SetNormalProgressAsync(0, _currentMessage);
            _ = _progressBar.SetStepsAsync(steps);

            NotifyProgressUpdate(_stepStates.Values);
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set stepped mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set stepped mode", ex));
        }
    }

    /// <summary>
    ///     Updates overall progress (0-100) in normal mode.
    ///     Fails if the bar is currently in indeterminate or stepped mode.
    /// </summary>
    /// <param name="progress">A value from 0 to 100 representing the progress percentage.</param>
    /// <param name="message">An optional message to display.</param>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    public async ValueTask<Result<Unit, ProgressManagerError>> UpdateProgressAsync(
        double progress,
        string? message = null)
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        if (_isIndeterminateMode)
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Cannot update progress in indeterminate mode"));
        }

        if (_isSteppedMode)
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Use UpdateStepProgressAsync in stepped mode"));
        }

        try
        {
            await _updateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _currentProgress = Math.Clamp(progress, 0, 100);
                if (!string.IsNullOrEmpty(message))
                {
                    _currentMessage = message;
                }

                // Update the bar using its asynchronous method
                await _progressBar!.SetNormalProgressAsync(_currentProgress, _currentMessage).ConfigureAwait(false);

                NotifyProgressUpdate();
                return Result<Unit, ProgressManagerError>.Success(Unit.Value);
            }
            finally
            {
                _updateLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to update progress", ex));
        }
    }

    /// <summary>
    ///     Updates progress for a specific step in stepped mode (including progress and status).
    ///     Fails if not in stepped mode.
    /// </summary>
    /// <param name="stepId">Unique identifier for the step.</param>
    /// <param name="progress">A value from 0 to 100 for the step's completion percentage.</param>
    /// <param name="status">The step's new status (e.g., InProgress, Completed).</param>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    public async ValueTask<Result<Unit, ProgressManagerError>> UpdateStepProgressAsync(
        string stepId,
        double progress,
        StepStatus status)
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        if (!_isSteppedMode)
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Cannot update step progress when not in stepped mode"));
        }

        try
        {
            await _updateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var stepUpdate = new StepUpdate(stepId, Math.Clamp(progress, 0, 100), status);
                _stepStates[stepId] = stepUpdate;

                // Update the UI asynchronously
                await _progressBar!.UpdateStepProgressAsync(stepId, stepUpdate.Progress, stepUpdate.Status)
                    .ConfigureAwait(false);

                // Optionally update the overall message (e.g. "Step 2 of 5")
                if (_steps is { Count: > 0 })
                {
                    // Avoid creating a new list
                    var stepList = _steps as List<ProgressStepConfig>
                                   ?? [.._steps];

                    var stepIndex = stepList.FindIndex(s => s.Id == stepId);
                    if (stepIndex >= 0)
                    {
                        _currentMessage = $"Step {stepIndex + 1} of {_steps.Count}";

                        // Keep the normal progress bar text consistent
                        await _progressBar.SetNormalProgressAsync(_currentProgress, _currentMessage)
                            .ConfigureAwait(false);
                    }
                }

                NotifyProgressUpdate([stepUpdate]);
                return Result<Unit, ProgressManagerError>.Success(Unit.Value);
            }
            finally
            {
                _updateLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update step progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to update step progress", ex));
        }
    }

    /// <summary>
    ///     Completes the current progress operation, marking all steps as 100% if in stepped mode,
    ///     then hides the progress bar after a short delay.
    /// </summary>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    public async ValueTask<Result<Unit, ProgressManagerError>> CompleteAsync()
    {
        const int completionDelayMs = 500; // minor optimization: use a named constant

        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        try
        {
            await _updateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_isSteppedMode)
                {
                    // Mark incomplete steps as completed
                    var updates = new List<StepUpdate>();
                    foreach (var (stepId, oldState) in _stepStates)
                    {
                        if (oldState.Status is not (StepStatus.Completed or StepStatus.Failed or StepStatus.Skipped))
                        {
                            var stepUpdate = new StepUpdate(stepId, 100, StepStatus.Completed);
                            _stepStates[stepId] = stepUpdate;
                            // Update bar UI
                            await _progressBar!.UpdateStepProgressAsync(stepId, 100, StepStatus.Completed)
                                .ConfigureAwait(false);
                            updates.Add(stepUpdate);
                        }
                    }

                    if (updates.Count > 0)
                    {
                        NotifyProgressUpdate(updates);
                    }
                }
                else
                {
                    // Normal mode
                    _currentProgress = 100;
                    _currentMessage = "Completed";
                    await _progressBar!.SetNormalProgressAsync(_currentProgress, _currentMessage)
                        .ConfigureAwait(false);
                    NotifyProgressUpdate();
                }

                // Small delay so user can see "Completed" state
                await Task.Delay(completionDelayMs).ConfigureAwait(false);

                // Reset the bar UI
                if (_progressBar != null)
                {
                    await _progressBar.SetNormalProgressAsync(0, string.Empty).ConfigureAwait(false);
                    await _progressBar.SetStepsAsync(null).ConfigureAwait(false);
                }

                _isVisible = false;
                NotifyProgressUpdate();
                return Result<Unit, ProgressManagerError>.Success(Unit.Value);
            }
            finally
            {
                _updateLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to complete progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to complete progress", ex));
        }
    }

    /// <summary>
    ///     Disposes this manager asynchronously, releasing all resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        DisableExecutionEngineIntegration(); // Unsubscribe from any MessagePipe integrations
        _updateLock.Dispose(); // Dispose semaphore
        _progressBar = null; // Remove reference

        await ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Enables integration with the ExecutionEngine via MessagePipe subscribers.
    /// </summary>
    /// <param name="channelId">The channel ID for subscribing to engine messages.</param>
    /// <param name="taskStartedSubscriber">Subscriber for <see cref="TaskStartedMessage" />.</param>
    /// <param name="taskProgressSubscriber">Subscriber for <see cref="TaskProgressMessage" />.</param>
    /// <param name="taskCompletedSubscriber">Subscriber for <see cref="TaskCompletedMessage" />.</param>
    /// <param name="taskFailedSubscriber">Subscriber for <see cref="TaskFailedMessage" />.</param>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    public Result<Unit, ProgressManagerError> EnableExecutionEngineIntegration(
        Guid channelId,
        IAsyncSubscriber<Guid, TaskStartedMessage> taskStartedSubscriber,
        IAsyncSubscriber<Guid, TaskProgressMessage> taskProgressSubscriber,
        IAsyncSubscriber<Guid, TaskCompletedMessage> taskCompletedSubscriber,
        IAsyncSubscriber<Guid, TaskFailedMessage> taskFailedSubscriber)
    {
        ThrowIfDisposed();

        try
        {
            DisableExecutionEngineIntegration();
            _bagBuilder = DisposableBag.CreateBuilder();

            // Subscribe to engine messages
            taskProgressSubscriber
                .Subscribe(channelId, async (message, ct) => await HandleTaskProgressAsync(message, ct))
                .AddTo(_bagBuilder);

            taskStartedSubscriber
                .Subscribe(channelId, async (message, ct) => await HandleTaskStartedAsync(message, ct))
                .AddTo(_bagBuilder);

            taskCompletedSubscriber
                .Subscribe(channelId, async (message, ct) => await HandleTaskCompletedAsync(message, ct))
                .AddTo(_bagBuilder);

            taskFailedSubscriber
                .Subscribe(channelId, async (message, ct) => await HandleTaskFailedAsync(message, ct))
                .AddTo(_bagBuilder);

            _disposableBag = _bagBuilder.Build();
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to enable execution engine integration");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to enable execution engine integration", ex));
        }
    }

    #region Private Integration Methods

    /// <summary>
    ///     Disables integration by disposing all engine event subscriptions.
    /// </summary>
    private void DisableExecutionEngineIntegration()
    {
        _disposableBag?.Dispose();
        _disposableBag = null;
        _bagBuilder = null;
    }

    private async Task HandleTaskProgressAsync(TaskProgressMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_isSteppedMode)
        {
            // If the engine is reporting step-level progress
            if (message.TaskProgressPercentage.HasValue)
            {
                await UpdateStepProgressAsync(
                    message.TaskName,
                    message.TaskProgressPercentage.Value,
                    StepStatus.InProgress).ConfigureAwait(false);
            }
        }
        else if (!_isIndeterminateMode && message.OverallProgressPercentage.HasValue)
        {
            // For normal mode, update overall progress
            await UpdateProgressAsync(
                message.OverallProgressPercentage.Value,
                message.Message).ConfigureAwait(false);
        }
    }

    private async Task HandleTaskStartedAsync(TaskStartedMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_isSteppedMode)
        {
            // Mark step as "In Progress" at 0% for visual feedback
            await UpdateStepProgressAsync(message.TaskName, 0, StepStatus.InProgress).ConfigureAwait(false);
        }
    }

    private async Task HandleTaskCompletedAsync(TaskCompletedMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_isSteppedMode)
        {
            await UpdateStepProgressAsync(message.TaskName, 100, StepStatus.Completed).ConfigureAwait(false);
        }
    }

    private async Task HandleTaskFailedAsync(TaskFailedMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_isSteppedMode)
        {
            await UpdateStepProgressAsync(message.TaskName, 0, StepStatus.Failed).ConfigureAwait(false);
        }
    }

    #endregion

    #region Helpers

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ExecutionProgressManager));
        }
    }

    private void EnsureProgressBarInitialized()
    {
        if (_progressBar == null)
        {
            throw new InvalidOperationException("Progress bar not initialized. Call Initialize first.");
        }
    }

    /// <summary>
    ///     Fires <see cref="OnProgressUpdated" /> with the current progress state.
    /// </summary>
    /// <param name="stepUpdates">An optional collection of step updates.</param>
    private void NotifyProgressUpdate(IEnumerable<StepUpdate>? stepUpdates = null)
    {
        OnProgressUpdated?.Invoke(
            new ProgressUpdate
            {
                IsVisible = _isVisible,
                IsIndeterminate = _isIndeterminateMode,
                Message = _currentMessage,
                Progress = _currentProgress,
                Steps = _steps,
                StepUpdates = stepUpdates?.ToList()
            }
        );
    }

    #endregion
}
