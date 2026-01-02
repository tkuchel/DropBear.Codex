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
///     Modern execution progress manager optimized for .NET 9 and Blazor Server.
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
    private readonly Lock _stateLock = new(); // .NET 9 Lock object - optimized for high-contention scenarios
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);
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

            // Fire and forget UI update using .NET 9 Task.Run optimizations
            _ = Task.Run(async () => await UpdateProgressBarAsync(indeterminate: true, message: message).ConfigureAwait(false),
                _cancellationTokenSource.Token);
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);
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

            _ = Task.Run(async () => await UpdateProgressBarAsync(progress: 0, message: _currentMessage).ConfigureAwait(false),
                _cancellationTokenSource.Token);
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);
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

                // Initialize step states efficiently using .NET 9 collection builders
                var stepStatesBuilder = new Dictionary<string, StepState>(steps.Count, StringComparer.Ordinal);
                foreach (var step in steps)
                {
                    stepStatesBuilder[step.Id] = new StepState(step.Id, 0, StepStatus.NotStarted);
                }

                _stepStates = stepStatesBuilder.ToFrozenDictionary();
            }

            _ = Task.Run(async () => await UpdateProgressBarAsync(steps: steps, progress: 0, message: _currentMessage).ConfigureAwait(false),
                _cancellationTokenSource.Token);
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);
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

            await UpdateProgressBarAsync(progress: clampedProgress, message: currentMessage).ConfigureAwait(false);
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);
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
            double overallProgress;

            lock (_stateLock)
            {
                if (!_stepStates.TryGetValue(stepId, out var existingStep))
                {
                    return Result<Unit, ProgressManagerError>.Failure(
                        new ProgressManagerError($"Step '{stepId}' not found"));
                }

                updatedStep = existingStep with { Progress = clampedProgress, Status = status };

                // Create new frozen dictionary with updated step using .NET 9 optimizations
                var stepStatesBuilder = new Dictionary<string, StepState>(_stepStates, StringComparer.Ordinal);
                stepStatesBuilder[stepId] = updatedStep;
                _stepStates = stepStatesBuilder.ToFrozenDictionary();

                // Update overall message and calculate overall progress
                if (_steps is { Count: > 0 })
                {
                    var stepIndex = _steps.ToList().FindIndex(s => string.Equals(s.Id, stepId, StringComparison.Ordinal));
                    if (stepIndex >= 0)
                    {
                        _currentMessage = $"Step {stepIndex + 1} of {_steps.Count}";
                    }

                    // Calculate overall progress as average of all step progress values
                    var totalProgress = _stepStates.Values.Sum(s => s.Progress);
                    overallProgress = totalProgress / _steps.Count;
                    _currentProgress = overallProgress;
                }
                else
                {
                    overallProgress = 0;
                }

                currentMessage = _currentMessage;
            }

            await UpdateStepInProgressBarAsync(stepId, clampedProgress, status).ConfigureAwait(false);
            await UpdateProgressBarAsync(progress: overallProgress, message: currentMessage).ConfigureAwait(false);

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
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        EnsureInitialized();

        try
        {
            var completedSteps = new List<StepState>();

            lock (_stateLock)
            {
                if (_currentMode == ProgressMode.Stepped)
                {
                    // Complete any incomplete steps using .NET 9 collection improvements
                    var stepStatesBuilder = new Dictionary<string, StepState>(_stepStates, StringComparer.Ordinal);
                    foreach (var (stepId, stepState) in _stepStates)
                    {
                        if (stepState.Status is not (StepStatus.Completed or StepStatus.Failed or StepStatus.Skipped))
                        {
                            var completedStep = stepState with { Progress = 100, Status = StepStatus.Completed };
                            stepStatesBuilder[stepId] = completedStep;
                            completedSteps.Add(completedStep);
                        }
                    }

                    _stepStates = stepStatesBuilder.ToFrozenDictionary();
                }
                else
                {
                    _currentProgress = 100;
                    _currentMessage = "Completed";
                }
            }

            // Update UI for completed steps using parallel processing where beneficial
            if (completedSteps.Count > 0)
            {
                await Task.WhenAll(completedSteps.Select(step =>
                    UpdateStepInProgressBarAsync(step.Id, step.Progress, step.Status).AsTask()));
            }

            if (_currentMode != ProgressMode.Stepped)
            {
                await UpdateProgressBarAsync(progress: 100, message: "Completed").ConfigureAwait(false);
            }

            NotifyProgressUpdate(completedSteps.Count > 0 ? completedSteps : null);

            // Brief delay to show completion state using .NET 9 optimized Task.Delay
            await Task.Delay(CompletionDisplayDuration, _cancellationTokenSource.Token).ConfigureAwait(false);

            // Reset the progress bar
            await ResetProgressBarAsync().ConfigureAwait(false);

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
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(taskStartedSubscriber);
        ArgumentNullException.ThrowIfNull(taskProgressSubscriber);
        ArgumentNullException.ThrowIfNull(taskCompletedSubscriber);
        ArgumentNullException.ThrowIfNull(taskFailedSubscriber);

        try
        {
            // Clean up existing subscriptions
            DisableExecutionEngineIntegration();

            _subscriptions = DisposableBag.CreateBuilder();

            // Subscribe to execution engine messages using .NET 9 optimized delegates
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

    private async ValueTask HandleTaskStartedAsync(TaskStartedMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested) return;

        if (_currentMode == ProgressMode.Stepped)
        {
            await UpdateStepProgressAsync(message.TaskName, 0, StepStatus.InProgress).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleTaskProgressAsync(TaskProgressMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested) return;

        switch (_currentMode)
        {
            case ProgressMode.Stepped when message.TaskProgressPercentage.HasValue:
                await UpdateStepProgressAsync(message.TaskName, message.TaskProgressPercentage.Value,
                    StepStatus.InProgress).ConfigureAwait(false);
                break;
            case ProgressMode.Normal when message.OverallProgressPercentage.HasValue:
                await UpdateProgressAsync(message.OverallProgressPercentage.Value, message.Message).ConfigureAwait(false);
                break;
        }
    }

    private async ValueTask HandleTaskCompletedAsync(TaskCompletedMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested) return;

        if (_currentMode == ProgressMode.Stepped)
        {
            await UpdateStepProgressAsync(message.TaskName, 100, StepStatus.Completed).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleTaskFailedAsync(TaskFailedMessage message, CancellationToken cancellationToken)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested) return;

        if (_currentMode == ProgressMode.Stepped)
        {
            await UpdateStepProgressAsync(message.TaskName, 0, StepStatus.Failed).ConfigureAwait(false);
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

    private async ValueTask UpdateProgressBarAsync(
        bool? indeterminate = null,
        double? progress = null,
        string? message = null,
        IReadOnlyList<ProgressStepConfig>? steps = null)
    {
        if (_progressBar == null || IsDisposed) return;

        try
        {
            // Use .NET 9 optimized cancellation token linking
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
            await _progressBar.UpdateProgressAsync(_progressBar.Progress, _progressBar.Message).ConfigureAwait(false);
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

    private async ValueTask UpdateStepInProgressBarAsync(string stepId, double progress, StepStatus status)
    {
        if (_progressBar == null || IsDisposed) return;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            cts.CancelAfter(OperationTimeout);

            await _progressBar.UpdateStepAsync(stepId, progress, status).ConfigureAwait(false);
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

    private async ValueTask ResetProgressBarAsync()
    {
        if (_progressBar == null || IsDisposed) return;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            cts.CancelAfter(OperationTimeout);

            await _progressBar.ResetAsync().ConfigureAwait(false);
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

            // Avoid redundant notifications using .NET 9 optimized equality comparison
            if (_lastProgressUpdate?.Equals(progressUpdate) == true) return;
            _lastProgressUpdate = progressUpdate;
        }

        // Safely invoke all event handlers using .NET 9 delegate optimizations
        var invocationList = handler.GetInvocationList();
        if (invocationList.Length == 1)
        {
            // Single handler - direct invocation
            try
            {
                handler(progressUpdate);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in OnProgressUpdated event handler");
            }
        }
        else
        {
            // Multiple handlers - parallel invocation for better performance
            Parallel.ForEach(invocationList.Cast<Action<ProgressUpdate>>(), singleHandler =>
            {
                try
                {
                    singleHandler(progressUpdate);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in OnProgressUpdated event handler");
                }
            });
        }
    }

    #endregion

    #region Disposal

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed) return;

        IsDisposed = true;

        // Cancel all operations using .NET 9 optimized cancellation
        await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);

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
