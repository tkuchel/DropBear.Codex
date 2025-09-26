#region

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Progress;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Errors;
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
///     Modern execution progress manager optimized for .NET 8+ and Blazor Server.
///     Bridges ExecutionEngine messages to DropBearProgressBar with improved performance and UX.
/// </summary>
public sealed class ExecutionProgressManager : IExecutionProgressManager
{
    #region Constants

    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CompletionDisplayDuration = TimeSpan.FromMilliseconds(750);

    #endregion

    #region Fields

    private readonly ILogger _logger;
    private readonly object _stateLock = new(); // Standard object lock for .NET 8 compatibility
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    // Component state
    private DropBearProgressBar? _progressBar;
    private ProgressMode _currentMode = ProgressMode.None;
    private double _currentProgress;
    private string _currentMessage = string.Empty;
    private IReadOnlyList<ProgressStepConfig>? _steps;

    // Step tracking with frozen collections for performance
    private FrozenDictionary<string, StepState> _stepStates = FrozenDictionary<string, StepState>.Empty;

    // MessagePipe integration
    private DisposableBagBuilder? _subscriptions;
    private IDisposable? _subscriptionBag;

    // Cached state for performance
    private ProgressUpdate? _lastProgressUpdate;
    private bool _isVisible;

    #endregion

    #region Constructor

    /// <summary>
    ///     Initializes a new instance of ExecutionProgressManager.
    /// </summary>
    public ExecutionProgressManager()
    {
        _logger = LoggerFactory.Logger.ForContext<ExecutionProgressManager>();
    }

    #endregion

    #region Properties

    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public event Action<ProgressUpdate>? OnProgressUpdated;

    #endregion

    #region Core API

    /// <inheritdoc />
    public Result<Unit, ProgressManagerError> Initialize(DropBearProgressBar progressBar)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(ExecutionProgressManager));
        ArgumentNullException.ThrowIfNull(progressBar);

        try
        {
            lock (_stateLock)
            {
                _progressBar = progressBar;
                _currentMode = ProgressMode.None;
                _currentProgress = 0;
                _currentMessage = string.Empty;
                _isVisible = false;
            }

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize progress manager");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to initialize progress manager", ex));
        }
    }

    /// <inheritdoc />
    public Result<Unit, ProgressManagerError> SetIndeterminateMode(string message)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(ExecutionProgressManager));
        EnsureInitialized();

        try
        {
            lock (_stateLock)
            {
                _currentMode = ProgressMode.Indeterminate;
                _currentMessage = message;
                _currentProgress = 0;
                _isVisible = true;
                _steps = null;
                _stepStates = FrozenDictionary<string, StepState>.Empty;
            }

            // Fire and forget UI update
            _ = UpdateProgressBarAsync(indeterminate: true, message: message);
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

    /// <inheritdoc />
    public Result<Unit, ProgressManagerError> SetNormalMode()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(ExecutionProgressManager));
        EnsureInitialized();

        try
        {
            lock (_stateLock)
            {
                _currentMode = ProgressMode.Normal;
                _currentMessage = "Starting...";
                _currentProgress = 0;
                _isVisible = true;
                _steps = null;
                _stepStates = FrozenDictionary<string, StepState>.Empty;
            }

            _ = UpdateProgressBarAsync(progress: 0, message: _currentMessage);
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

    /// <inheritdoc />
    public Result<Unit, ProgressManagerError> SetSteppedMode(IReadOnlyList<ProgressStepConfig> steps)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(ExecutionProgressManager));
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(steps);

        try
        {
            lock (_stateLock)
            {
                _currentMode = ProgressMode.Stepped;
                _steps = steps;
                _isVisible = true;
                _currentProgress = 0;
                _currentMessage = steps.Count > 0 ? $"Step 1 of {steps.Count}" : "No steps";

                // Initialize step states efficiently
                var stepStatesDict = new Dictionary<string, StepState>(steps.Count);
                foreach (var step in steps)
                {
                    stepStatesDict[step.Id] = new StepState(step.Id, 0, StepStatus.NotStarted);
                }
                _stepStates = stepStatesDict.ToFrozenDictionary();
            }

            _ = UpdateProgressBarAsync(steps: steps, progress: 0, message: _currentMessage);
            NotifyProgressUpdate();

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set stepped mode");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to set stepped mode", ex));
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit, ProgressManagerError>> UpdateProgressAsync(
        double progress,
        string? message = null)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(ExecutionProgressManager));
        EnsureInitialized();

        if (_currentMode == ProgressMode.Indeterminate)
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Cannot update progress in indeterminate mode"));
        }

        if (_currentMode == ProgressMode.Stepped)
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Use UpdateStepProgressAsync in stepped mode"));
        }

        try
        {
            var clampedProgress = Math.Clamp(progress, 0, 100);
            string currentMessage;

            lock (_stateLock)
            {
                _currentProgress = clampedProgress;
                if (!string.IsNullOrEmpty(message))
                {
                    _currentMessage = message;
                }
                currentMessage = _currentMessage;
            }

            await UpdateProgressBarAsync(progress: clampedProgress, message: currentMessage);
            NotifyProgressUpdate();

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to update progress", ex));
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit, ProgressManagerError>> UpdateStepProgressAsync(
        string stepId,
        double progress,
        StepStatus status)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(ExecutionProgressManager));
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrEmpty(stepId);

        if (_currentMode != ProgressMode.Stepped)
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Cannot update step progress when not in stepped mode"));
        }

        try
        {
            var clampedProgress = Math.Clamp(progress, 0, 100);
            StepState updatedStep;
            string currentMessage;

            lock (_stateLock)
            {
                if (!_stepStates.TryGetValue(stepId, out var existingStep))
                {
                    return Result<Unit, ProgressManagerError>.Failure(
                        new ProgressManagerError($"Step '{stepId}' not found"));
                }

                updatedStep = existingStep with { Progress = clampedProgress, Status = status };

                // Create new frozen dictionary with updated step
                var stepStatesDict = _stepStates.ToDictionary();
                stepStatesDict[stepId] = updatedStep;
                _stepStates = stepStatesDict.ToFrozenDictionary();

                // Update overall message
                if (_steps != null && _steps.Count > 0)
                {
                    var stepIndex = _steps.ToList().FindIndex(s => s.Id == stepId);
                    if (stepIndex >= 0)
                    {
                        _currentMessage = $"Step {stepIndex + 1} of {_steps.Count}";
                    }
                }
                currentMessage = _currentMessage;
            }

            await UpdateStepInProgressBarAsync(stepId, clampedProgress, status);
            await UpdateProgressBarAsync(message: currentMessage);

            NotifyProgressUpdate([updatedStep]);

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update step progress for step {StepId}", stepId);
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError($"Failed to update step progress for step '{stepId}'", ex));
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit, ProgressManagerError>> CompleteAsync()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(ExecutionProgressManager));
        EnsureInitialized();

        try
        {
            var completedSteps = new List<StepState>();

            lock (_stateLock)
            {
                if (_currentMode == ProgressMode.Stepped)
                {
                    // Complete any incomplete steps
                    var stepStatesDict = _stepStates.ToDictionary();
                    foreach (var (stepId, stepState) in _stepStates)
                    {
                        if (stepState.Status is not (StepStatus.Completed or StepStatus.Failed or StepStatus.Skipped))
                        {
                            var completedStep = stepState with { Progress = 100, Status = StepStatus.Completed };
                            stepStatesDict[stepId] = completedStep;
                            completedSteps.Add(completedStep);
                        }
                    }
                    _stepStates = stepStatesDict.ToFrozenDictionary();
                }
                else
                {
                    _currentProgress = 100;
                    _currentMessage = "Completed";
                }
            }

            // Update UI for completed steps
            foreach (var step in completedSteps)
            {
                await UpdateStepInProgressBarAsync(step.Id, step.Progress, step.Status);
            }

            if (_currentMode != ProgressMode.Stepped)
            {
                await UpdateProgressBarAsync(progress: 100, message: "Completed");
            }

            NotifyProgressUpdate(completedSteps.Count > 0 ? completedSteps : null);

            // Brief delay to show completion state
            await Task.Delay(CompletionDisplayDuration, _cancellationTokenSource.Token);

            // Reset the progress bar
            await ResetProgressBarAsync();

            lock (_stateLock)
            {
                _isVisible = false;
            }

            NotifyProgressUpdate();

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.Token.IsCancellationRequested)
        {
            // Expected during disposal
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to complete progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to complete progress", ex));
        }
    }

    #endregion

    #region ExecutionEngine Integration

    /// <inheritdoc />
    public Result<Unit, ProgressManagerError> EnableExecutionEngineIntegration(
        Guid channelId,
        IAsyncSubscriber<Guid, TaskStartedMessage> taskStartedSubscriber,
        IAsyncSubscriber<Guid, TaskProgressMessage> taskProgressSubscriber,
        IAsyncSubscriber<Guid, TaskCompletedMessage> taskCompletedSubscriber,
        IAsyncSubscriber<Guid, TaskFailedMessage> taskFailedSubscriber)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(ExecutionProgressManager));
        ArgumentNullException.ThrowIfNull(taskStartedSubscriber);
        ArgumentNullException.ThrowIfNull(taskProgressSubscriber);
        ArgumentNullException.ThrowIfNull(taskCompletedSubscriber);
        ArgumentNullException.ThrowIfNull(taskFailedSubscriber);

        try
        {
            // Clean up existing subscriptions
            DisableExecutionEngineIntegration();

            _subscriptions = DisposableBag.CreateBuilder();

            // Subscribe to execution engine messages
            taskStartedSubscriber
                .Subscribe(channelId, (message, ct) => HandleTaskStartedAsync(message, ct))
                .AddTo(_subscriptions);

            taskProgressSubscriber
                .Subscribe(channelId, (message, ct) => HandleTaskProgressAsync(message, ct))
                .AddTo(_subscriptions);

            taskCompletedSubscriber
                .Subscribe(channelId, (message, ct) => HandleTaskCompletedAsync(message, ct))
                .AddTo(_subscriptions);

            taskFailedSubscriber
                .Subscribe(channelId, (message, ct) => HandleTaskFailedAsync(message, ct))
                .AddTo(_subscriptions);

            _subscriptionBag = _subscriptions.Build();

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to enable execution engine integration");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to enable execution engine integration", ex));
        }
    }

    private void DisableExecutionEngineIntegration()
    {
        _subscriptionBag?.Dispose();
        _subscriptionBag = null;
        _subscriptions = null;
    }

    #endregion

    #region Message Handlers

    private async Task HandleTaskStartedAsync(TaskStartedMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested) return;

        if (_currentMode == ProgressMode.Stepped)
        {
            await UpdateStepProgressAsync(message.TaskName, 0, StepStatus.InProgress);
        }
    }

    private async Task HandleTaskProgressAsync(TaskProgressMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested) return;

        switch (_currentMode)
        {
            case ProgressMode.Stepped when message.TaskProgressPercentage.HasValue:
                await UpdateStepProgressAsync(message.TaskName, message.TaskProgressPercentage.Value, StepStatus.InProgress);
                break;
            case ProgressMode.Normal when message.OverallProgressPercentage.HasValue:
                await UpdateProgressAsync(message.OverallProgressPercentage.Value, message.Message);
                break;
        }
    }

    private async Task HandleTaskCompletedAsync(TaskCompletedMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested) return;

        if (_currentMode == ProgressMode.Stepped)
        {
            await UpdateStepProgressAsync(message.TaskName, 100, StepStatus.Completed);
        }
    }

    private async Task HandleTaskFailedAsync(TaskFailedMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested) return;

        if (_currentMode == ProgressMode.Stepped)
        {
            await UpdateStepProgressAsync(message.TaskName, 0, StepStatus.Failed);
        }
    }

    #endregion

    #region Helper Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureInitialized()
    {
        if (_progressBar == null)
        {
            throw new InvalidOperationException("Progress bar not initialized. Call Initialize first.");
        }
    }

    private async Task UpdateProgressBarAsync(
        bool? indeterminate = null,
        double? progress = null,
        string? message = null,
        IReadOnlyList<ProgressStepConfig>? steps = null)
    {
        if (_progressBar == null || IsDisposed) return;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            cts.CancelAfter(OperationTimeout);

            // Update progress bar properties directly
            if (indeterminate == true)
            {
                _progressBar.IsIndeterminate = true;
                if (message != null) _progressBar.Message = message;
            }
            else
            {
                _progressBar.IsIndeterminate = false;
                if (progress.HasValue) _progressBar.Progress = progress.Value;
                if (message != null) _progressBar.Message = message;
                if (steps != null) _progressBar.Steps = steps;
            }

            // Force UI refresh through component's public method
            await _progressBar.UpdateProgressAsync(_progressBar.Progress, _progressBar.Message);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.Token.IsCancellationRequested)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to update progress bar UI");
        }
    }

    private async Task UpdateStepInProgressBarAsync(string stepId, double progress, StepStatus status)
    {
        if (_progressBar == null || IsDisposed) return;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            cts.CancelAfter(OperationTimeout);

            await _progressBar.UpdateStepAsync(stepId, progress, status);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.Token.IsCancellationRequested)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to update step {StepId} in progress bar", stepId);
        }
    }

    private async Task ResetProgressBarAsync()
    {
        if (_progressBar == null || IsDisposed) return;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            cts.CancelAfter(OperationTimeout);

            await _progressBar.ResetAsync();
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.Token.IsCancellationRequested)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to reset progress bar");
        }
    }

    private void NotifyProgressUpdate(IReadOnlyList<StepState>? stepUpdates = null)
    {
        var handler = OnProgressUpdated;
        if (handler == null) return;

        ProgressUpdate progressUpdate;
        lock (_stateLock)
        {
            progressUpdate = new ProgressUpdate
            {
                IsVisible = _isVisible,
                IsIndeterminate = _currentMode == ProgressMode.Indeterminate,
                Message = _currentMessage,
                Progress = _currentProgress,
                Steps = _steps,
                StepUpdates = stepUpdates?.Select(s => new StepUpdate(s.Id, s.Progress, s.Status)).ToList()
            };

            // Avoid redundant notifications
            if (_lastProgressUpdate?.Equals(progressUpdate) == true) return;
            _lastProgressUpdate = progressUpdate;
        }

        // Safely invoke all event handlers
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

    #endregion

    #region Disposal

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed) return;

        IsDisposed = true;

        // Cancel all operations
        await _cancellationTokenSource.CancelAsync();

        // Clean up subscriptions
        DisableExecutionEngineIntegration();

        // Dispose resources
        _cancellationTokenSource.Dispose();

        // Clear state
        lock (_stateLock)
        {
            _progressBar = null;
            _steps = null;
            _stepStates = FrozenDictionary<string, StepState>.Empty;
            _lastProgressUpdate = null;
        }
    }

    #endregion

    #region Supporting Types

    private enum ProgressMode
    {
        None,
        Indeterminate,
        Normal,
        Stepped
    }

    private sealed record StepState(string Id, double Progress, StepStatus Status);

    #endregion
}
