#region

using System.Collections.Concurrent;
using System.Runtime.Versioning;
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
///     Handles indeterminate, normal, and stepped progress modes without directly writing to the bar's parameters.
/// </summary>
public sealed class ExecutionProgressManager : IExecutionProgressManager
{
    #region Constructors

    /// <summary>
    ///     Creates a new <see cref="ExecutionProgressManager" /> instance.
    /// </summary>
    public ExecutionProgressManager()
    {
        _logger = LoggerFactory.Logger.ForContext<ExecutionProgressManager>();
    }

    #endregion

    #region Private Fields and Constants

    private readonly ILogger _logger;

    /// <summary>
    ///     Thread-safe dictionary holding step states for stepped mode.
    /// </summary>
    private readonly ConcurrentDictionary<string, StepUpdate> _stepStates = new();

    /// <summary>
    ///     Semaphore lock to protect critical sections.
    /// </summary>
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    // Timeouts for lock acquisition and progress bar operations.
    private static readonly TimeSpan UpdateLockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ProgressBarOperationTimeout = TimeSpan.FromSeconds(10);

    // Holds disposable resources from MessagePipe subscriptions.
    private DisposableBagBuilder? _bagBuilder;
    private IDisposable? _disposableBag;

    // Internal state fields
    private string _currentMessage = string.Empty;
    private double _currentProgress;
    private bool _isIndeterminateMode;
    private bool _isSteppedMode;
    private bool _isVisible;
    private DropBearProgressBar? _progressBar;
    private IReadOnlyList<ProgressStepConfig>? _steps;

    #endregion

    #region Public Properties and Events

    /// <summary>
    ///     Indicates whether this manager has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    ///     Event fired whenever the manager updates progress, providing a <see cref="ProgressUpdate" />.
    /// </summary>
    public event Action<ProgressUpdate>? OnProgressUpdated;

    #endregion

    #region Public Methods

    /// <summary>
    ///     Initializes the manager with a target <see cref="DropBearProgressBar" /> instance.
    ///     Must be called before other methods are used.
    /// </summary>
    /// <param name="progressBar">The progress bar component to control.</param>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when progressBar is null.</exception>
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
    [UnsupportedOSPlatform("browser")]
    public Result<Unit, ProgressManagerError> SetIndeterminateMode(string message)
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        // Acquire lock synchronously for state update.
        if (!_updateLock.Wait(UpdateLockTimeout))
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Timed out waiting for lock in SetIndeterminateMode"));
        }

        try
        {
            _isIndeterminateMode = true;
            _isSteppedMode = false;
            _currentMessage = message;
            _currentProgress = 0;
            _isVisible = true;
            _steps = null;

            // Fire-and-forget async call with timeout protection.
            FireAndForget(() => _progressBar!.SetIndeterminateModeAsync(message), "SetIndeterminateModeAsync");
            NotifyProgressUpdate();
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set indeterminate mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set indeterminate mode", ex));
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    ///     Sets the progress bar to normal (determinate) mode without discrete steps.
    /// </summary>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    [UnsupportedOSPlatform("browser")]
    public Result<Unit, ProgressManagerError> SetNormalMode()
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        if (!_updateLock.Wait(UpdateLockTimeout))
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Timed out waiting for lock in SetNormalMode"));
        }

        try
        {
            _isIndeterminateMode = false;
            _isSteppedMode = false;
            _currentMessage = "Starting...";
            _currentProgress = 0;
            _isVisible = true;
            _steps = null;

            // Fire-and-forget UI update calls with timeout protection.
            FireAndForget(() => _progressBar!.SetNormalProgressAsync(0, _currentMessage), "SetNormalProgressAsync");
            NotifyProgressUpdate();
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set normal mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set normal mode", ex));
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    ///     Sets the progress bar to stepped mode with a provided list of step configurations.
    /// </summary>
    /// <param name="steps">A read-only list of step configurations.</param>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    [UnsupportedOSPlatform("browser")]
    public Result<Unit, ProgressManagerError> SetSteppedMode(IReadOnlyList<ProgressStepConfig> steps)
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        if (!_updateLock.Wait(UpdateLockTimeout))
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Timed out waiting for lock in SetSteppedMode"));
        }

        try
        {
            _isIndeterminateMode = false;
            _isSteppedMode = true;
            _isVisible = true;
            _currentMessage = steps.Count > 0 ? $"Step 1 of {steps.Count}" : "No steps";
            _currentProgress = 0;
            _steps = steps;

            // Clear any existing step states and create new ones.
            _stepStates.Clear();
            foreach (var step in steps)
            {
                var stepUpdate = new StepUpdate(step.Id, 0, StepStatus.NotStarted);
                _stepStates[step.Id] = stepUpdate;
            }

            // Initialize the bar in normal mode and set steps.
            FireAndForget(() => _progressBar!.SetNormalProgressAsync(0, _currentMessage), "SetNormalProgressAsync");
            FireAndForget(() => _progressBar!.SetStepsAsync(steps), "SetStepsAsync");

            NotifyProgressUpdate(_stepStates.Values);
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set stepped mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set stepped mode", ex));
        }
        finally
        {
            _updateLock.Release();
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

        // Acquire the lock asynchronously with a timeout.
        if (!await _updateLock.WaitAsync(UpdateLockTimeout).ConfigureAwait(false))
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Timed out waiting for lock in UpdateProgressAsync"));
        }

        try
        {
            _currentProgress = Math.Clamp(progress, 0, 100);
            if (!string.IsNullOrEmpty(message))
            {
                _currentMessage = message;
            }

            // Await the UI update with timeout support.
            await ExecuteWithTimeoutAsync(
                () => _progressBar!.SetNormalProgressAsync(_currentProgress, _currentMessage),
                ProgressBarOperationTimeout,
                "SetNormalProgressAsync").ConfigureAwait(false);

            NotifyProgressUpdate();
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (TimeoutException tex)
        {
            _logger.Error(tex, "Operation timed out in UpdateProgressAsync");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Operation timed out in UpdateProgressAsync", tex));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to update progress", ex));
        }
        finally
        {
            _updateLock.Release();
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

        if (!await _updateLock.WaitAsync(UpdateLockTimeout).ConfigureAwait(false))
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Timed out waiting for lock in UpdateStepProgressAsync"));
        }

        try
        {
            var clampedProgress = Math.Clamp(progress, 0, 100);
            var stepUpdate = new StepUpdate(stepId, clampedProgress, status);
            _stepStates[stepId] = stepUpdate;

            // Await the UI update for the step with timeout support.
            await ExecuteWithTimeoutAsync(
                () => _progressBar!.UpdateStepProgressAsync(stepId, clampedProgress, status),
                ProgressBarOperationTimeout,
                "UpdateStepProgressAsync").ConfigureAwait(false);

            // Optionally update the overall message (e.g., "Step 2 of 5")
            if (_steps is { Count: > 0 })
            {
                // Use a local copy to avoid multiple enumerations.
                var stepList = _steps is List<ProgressStepConfig> list ? list : _steps.ToList();
                var stepIndex = stepList.FindIndex(s => s.Id == stepId);
                if (stepIndex >= 0)
                {
                    _currentMessage = $"Step {stepIndex + 1} of {_steps.Count}";
                    await ExecuteWithTimeoutAsync(
                        () => _progressBar?.SetNormalProgressAsync(_currentProgress, _currentMessage) ?? throw new InvalidOperationException(),
                        ProgressBarOperationTimeout,
                        "SetNormalProgressAsync").ConfigureAwait(false);
                }
            }

            NotifyProgressUpdate([stepUpdate]);
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (TimeoutException tex)
        {
            _logger.Error(tex, "Operation timed out in UpdateStepProgressAsync");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Operation timed out in UpdateStepProgressAsync", tex));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update step progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to update step progress", ex));
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    ///     Completes the current progress operation, marking all steps as 100% if in stepped mode,
    ///     then hides the progress bar after a short delay.
    /// </summary>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    public async ValueTask<Result<Unit, ProgressManagerError>> CompleteAsync()
    {
        const int completionDelayMs = 500; // Delay to allow the "Completed" state to be visible

        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        if (!await _updateLock.WaitAsync(UpdateLockTimeout).ConfigureAwait(false))
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Timed out waiting for lock in CompleteAsync"));
        }

        try
        {
            if (_isSteppedMode)
            {
                // Mark incomplete steps as completed.
                var updates = new List<StepUpdate>();
                foreach (var (stepId, oldState) in _stepStates)
                {
                    if (oldState.Status is not (StepStatus.Completed or StepStatus.Failed or StepStatus.Skipped))
                    {
                        var stepUpdate = new StepUpdate(stepId, 100, StepStatus.Completed);
                        _stepStates[stepId] = stepUpdate;
                        await ExecuteWithTimeoutAsync(
                            () => _progressBar!.UpdateStepProgressAsync(stepId, 100, StepStatus.Completed),
                            ProgressBarOperationTimeout,
                            "UpdateStepProgressAsync").ConfigureAwait(false);
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
                // Normal mode: update progress to 100%.
                _currentProgress = 100;
                _currentMessage = "Completed";
                await ExecuteWithTimeoutAsync(
                    () => _progressBar!.SetNormalProgressAsync(_currentProgress, _currentMessage),
                    ProgressBarOperationTimeout,
                    "SetNormalProgressAsync").ConfigureAwait(false);
                NotifyProgressUpdate();
            }

            // Delay briefly so that the user can see the "Completed" state.
            await Task.Delay(completionDelayMs).ConfigureAwait(false);

            // Reset the progress bar UI.
            if (_progressBar != null)
            {
                await ExecuteWithTimeoutAsync(
                    () => _progressBar.SetNormalProgressAsync(0, string.Empty),
                    ProgressBarOperationTimeout,
                    "SetNormalProgressAsync").ConfigureAwait(false);
                await ExecuteWithTimeoutAsync(
                    () => _progressBar.SetStepsAsync(null),
                    ProgressBarOperationTimeout,
                    "SetStepsAsync").ConfigureAwait(false);
            }

            _isVisible = false;
            NotifyProgressUpdate();
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (TimeoutException tex)
        {
            _logger.Error(tex, "Operation timed out in CompleteAsync");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Operation timed out in CompleteAsync", tex));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to complete progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to complete progress", ex));
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    ///     Disposes this manager asynchronously, releasing all resources.
    /// </summary>
    /// <returns>A <see cref="ValueTask" /> representing the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;

        // Unsubscribe from any MessagePipe integrations.
        DisableExecutionEngineIntegration();

        // Dispose of the semaphore.
        _updateLock.Dispose();

        // Clear references.
        _progressBar = null;
        _stepStates.Clear();

        // Complete any additional asynchronous cleanup.
        await Task.CompletedTask.ConfigureAwait(false);
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
            // Unsubscribe any previous subscriptions.
            DisableExecutionEngineIntegration();
            _bagBuilder = DisposableBag.CreateBuilder();

            // Subscribe to engine messages.
            taskProgressSubscriber
                .Subscribe(channelId,
                    async (message, ct) => await HandleTaskProgressAsync(message, ct).ConfigureAwait(false))
                .AddTo(_bagBuilder);

            taskStartedSubscriber
                .Subscribe(channelId,
                    async (message, ct) => await HandleTaskStartedAsync(message, ct).ConfigureAwait(false))
                .AddTo(_bagBuilder);

            taskCompletedSubscriber
                .Subscribe(channelId,
                    async (message, ct) => await HandleTaskCompletedAsync(message, ct).ConfigureAwait(false))
                .AddTo(_bagBuilder);

            taskFailedSubscriber
                .Subscribe(channelId,
                    async (message, ct) => await HandleTaskFailedAsync(message, ct).ConfigureAwait(false))
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

    #endregion

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

    /// <summary>
    ///     Throws an <see cref="ObjectDisposedException" /> if this manager has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ExecutionProgressManager));
        }
    }

    /// <summary>
    ///     Throws an <see cref="InvalidOperationException" /> if the progress bar has not been initialized.
    /// </summary>
    private void EnsureProgressBarInitialized()
    {
        if (_progressBar == null)
        {
            throw new InvalidOperationException("Progress bar not initialized. Call Initialize first.");
        }
    }

    /// <summary>
    ///     Fires the <see cref="OnProgressUpdated" /> event in a safe manner.
    ///     Each subscriber is invoked individually so that one faulty handler does not affect the others.
    /// </summary>
    /// <param name="stepUpdates">Optional collection of step updates.</param>
    private void NotifyProgressUpdate(IEnumerable<StepUpdate>? stepUpdates = null)
    {
        var handler = OnProgressUpdated;
        if (handler == null)
        {
            return;
        }

        // Capture current state in local variables for thread safety.
        var progressUpdate = new ProgressUpdate
        {
            IsVisible = _isVisible,
            IsIndeterminate = _isIndeterminateMode,
            Message = _currentMessage,
            Progress = _currentProgress,
            Steps = _steps,
            StepUpdates = stepUpdates?.ToList()
        };

        // Invoke each subscriber separately.
        foreach (var singleHandler in handler.GetInvocationList().Cast<Action<ProgressUpdate>>())
        {
            try
            {
                singleHandler(progressUpdate);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in OnProgressUpdated event handler");
            }
        }
    }

    /// <summary>
    ///     Executes an asynchronous operation with a timeout. If the operation does not complete
    ///     within the specified timeout, a <see cref="TimeoutException" /> is thrown.
    /// </summary>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="operationName">A name for the operation (used in exception messages).</param>
    /// <returns>A <see cref="Task" /> representing the operation.</returns>
    /// <exception cref="TimeoutException">Thrown if the operation times out.</exception>
    private async Task ExecuteWithTimeoutAsync(Func<Task> operation, TimeSpan timeout, string operationName)
    {
        var task = operation();
        var delayTask = Task.Delay(timeout);
        if (await Task.WhenAny(task, delayTask).ConfigureAwait(false) == delayTask)
        {
            throw new TimeoutException($"{operationName} timed out after {timeout.TotalSeconds} seconds.");
        }

        await task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Executes an asynchronous operation in a fire-and-forget manner.
    ///     Any exceptions (including timeouts) are caught and logged.
    /// </summary>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="operationName">A name for the operation (used in logging).</param>
    private void FireAndForget(Func<Task> operation, string operationName)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteWithTimeoutAsync(operation, ProgressBarOperationTimeout, operationName)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"{operationName} failed or timed out.");
            }
        });
    }

    #endregion
}
