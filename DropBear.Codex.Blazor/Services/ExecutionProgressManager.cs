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

    // Keep step states if you want them for some extra UI or logic
    private readonly ConcurrentDictionary<string, StepUpdate> _stepStates = new();
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    // MessagePipe clean-up
    private DisposableBagBuilder? _bagBuilder;
    private string _currentMessage = string.Empty;

    private double _currentProgress;
    private IDisposable? _disposableBag;
    private bool _isIndeterminateMode;
    private bool _isSteppedMode;
    private bool _isVisible;

    // Flags / data
    private DropBearProgressBar? _progressBar;
    private IReadOnlyList<ProgressStepConfig>? _steps;

    /// <summary>
    ///     Constructs a new instance of <see cref="ExecutionProgressManager" />.
    /// </summary>
    public ExecutionProgressManager()
    {
        _logger = LoggerFactory.Logger.ForContext<ExecutionProgressManager>();
    }

    /// <summary>
    ///     A flag indicating whether this manager has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    ///     Event fired whenever the manager updates progress (if needed by external observers).
    /// </summary>
    public event Action<ProgressUpdate>? OnProgressUpdated;

    /// <summary>
    ///     Initializes the manager with a target <see cref="DropBearProgressBar" /> instance.
    ///     Must be called before other methods.
    /// </summary>
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
    ///     Sets the progress bar to indeterminate mode.
    /// </summary>
    /// <param name="message">Message displayed to the user.</param>
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

            // Call the public method on DropBearProgressBar
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
    ///     Sets the progress bar to normal mode (non-indeterminate, but no discrete steps).
    /// </summary>
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

            // Use the bar's public method
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
    ///     Sets the progress bar to stepped mode, using a list of step configurations.
    /// </summary>
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

            // Initialize our local step states
            _stepStates.Clear();
            foreach (var step in steps)
            {
                var stepUpdate = new StepUpdate(step.Id, 0, StepStatus.NotStarted);
                _stepStates[step.Id] = stepUpdate;
            }

            // Set both normal progress (0) and the steps in the bar
            // Option A: call two separate methods
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
    ///     Updates the overall progress (0-100) in normal mode. Fails if in indeterminate or stepped mode.
    /// </summary>
    /// <param name="progress">A value from 0 to 100.</param>
    /// <param name="message">Optional message to display.</param>
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
            await _updateLock.WaitAsync();
            try
            {
                _currentProgress = Math.Clamp(progress, 0, 100);
                if (!string.IsNullOrEmpty(message))
                {
                    _currentMessage = message;
                }

                // Update the bar using its method
                await _progressBar!.SetNormalProgressAsync(_currentProgress, _currentMessage);

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
    ///     Updates a specific step in stepped mode (progress and status).
    /// </summary>
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
            await _updateLock.WaitAsync();
            try
            {
                // Update local dictionary
                var stepUpdate = new StepUpdate(stepId, Math.Clamp(progress, 0, 100), status);
                _stepStates[stepId] = stepUpdate;

                // Also call the bar's new method so UI is updated
                await _progressBar!.UpdateStepProgressAsync(stepId, stepUpdate.Progress, stepUpdate.Status);

                // Optionally adjust the bar's message to reflect the current step
                if (_steps is { Count: > 0 })
                {
                    var stepIndex = _steps.ToList().FindIndex(s => s.Id == stepId);
                    if (stepIndex >= 0)
                    {
                        _currentMessage = $"Step {stepIndex + 1} of {_steps.Count}";
                        // Then optionally also set the bar to the new message if desired:
                        await _progressBar.SetNormalProgressAsync(_currentProgress, _currentMessage);
                    }
                }

                NotifyProgressUpdate(new[] { stepUpdate });
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
    ///     Called when all progress completes; marks everything as 100% and hides the bar after a short delay.
    /// </summary>
    public async ValueTask<Result<Unit, ProgressManagerError>> CompleteAsync()
    {
        ThrowIfDisposed();
        EnsureProgressBarInitialized();

        try
        {
            await _updateLock.WaitAsync();
            try
            {
                if (_isSteppedMode)
                {
                    // Mark incomplete steps as Completed
                    var updates = new List<StepUpdate>();
                    foreach (var (stepId, oldState) in _stepStates)
                    {
                        if (oldState.Status is not (StepStatus.Completed or StepStatus.Failed or StepStatus.Skipped))
                        {
                            var stepUpdate = new StepUpdate(stepId, 100, StepStatus.Completed);
                            _stepStates[stepId] = stepUpdate;
                            // Update bar
                            await _progressBar!.UpdateStepProgressAsync(stepId, 100, StepStatus.Completed);
                            updates.Add(stepUpdate);
                        }
                    }

                    if (updates.Any())
                    {
                        NotifyProgressUpdate(updates);
                    }
                }
                else
                {
                    // Normal mode
                    _currentProgress = 100;
                    _currentMessage = "Completed";
                    await _progressBar!.SetNormalProgressAsync(_currentProgress, _currentMessage);
                    NotifyProgressUpdate();
                }

                // Hide progress bar after a small delay so user sees final state
                await Task.Delay(500);

                // If you want to forcibly reset the bar state:
                await _progressBar.SetNormalProgressAsync(0, string.Empty);
                await _progressBar.SetStepsAsync(null);

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
    ///     Disposes this manager asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        DisableExecutionEngineIntegration();
        _updateLock.Dispose();
        _progressBar = null;

        await ValueTask.CompletedTask;
    }

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

            // Example of subscribing with an async handler
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

    /// <summary>
    ///     Disables integration (unsubscribes from all engine events).
    /// </summary>
    private void DisableExecutionEngineIntegration()
    {
        _disposableBag?.Dispose();
        _disposableBag = null;
        _bagBuilder = null;
    }

    #region MessagePipe Handlers

    private async Task HandleTaskProgressAsync(TaskProgressMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_isSteppedMode)
        {
            // If the engine is reporting per-step progress
            if (message.TaskProgressPercentage.HasValue)
            {
                await UpdateStepProgressAsync(
                    message.TaskName,
                    message.TaskProgressPercentage.Value,
                    StepStatus.InProgress);
            }
        }
        else if (!_isIndeterminateMode && message.OverallProgressPercentage.HasValue)
        {
            // For normal mode, update overall progress
            await UpdateProgressAsync(
                message.OverallProgressPercentage.Value,
                message.Message);
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
            // Mark step as "In Progress" at 0% to visually show that it's running
            await UpdateStepProgressAsync(message.TaskName, 0, StepStatus.InProgress);
        }
        // If normal mode, do nothing or set message
    }

    private async Task HandleTaskCompletedAsync(TaskCompletedMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_isSteppedMode)
        {
            // Mark that step as completed
            await UpdateStepProgressAsync(message.TaskName, 100, StepStatus.Completed);
        }
        // If normal mode, you could do something else like set the progress to 100
    }

    private async Task HandleTaskFailedAsync(TaskFailedMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_isSteppedMode)
        {
            await UpdateStepProgressAsync(message.TaskName, 0, StepStatus.Failed);
        }
        // For normal mode, optionally set some "failed" state or do nothing
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
