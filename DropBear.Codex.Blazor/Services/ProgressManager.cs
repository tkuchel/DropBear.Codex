#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Thread-safe progress tracking manager optimized for Blazor Server with
///     enhanced memory usage and performance characteristics.
/// </summary>
public sealed class ProgressManager : IProgressManager
{
    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProgressManager" /> class.
    /// </summary>
    public ProgressManager()
    {
        _logger = LoggerFactory.Logger.ForContext<ProgressManager>();
        _disposalCts = new CancellationTokenSource();
        _stepStates = new ConcurrentDictionary<string, StepState>(
            Environment.ProcessorCount * 2,
            20,
            StringComparer.Ordinal); // Assume ~20 steps max in typical scenarios
        _taskProgress = new ConcurrentDictionary<string, double>(
            StringComparer.Ordinal);
        _updateLock = new SemaphoreSlim(1, 1);

        // Use a Timer instead of PeriodicTimer for better control and less overhead
        _progressTimer = new Timer(
            UpdateProgress,
            null,
            Timeout.Infinite,
            Timeout.Infinite);

        _logger.Debug("Progress manager initialized");
    }

    #endregion

    #region Events

    /// <summary>
    ///     Occurs when progress state changes.
    /// </summary>
    public event Func<Task>? StateChanged;

    #endregion

    #region Fields and Constants

    /// <summary>
    ///     Default timer interval in milliseconds.
    /// </summary>
    private const double DefaultTimerIntervalMs = 100;

    /// <summary>
    ///     Default progress increment per timer tick.
    /// </summary>
    private const double DefaultProgressIncrement = 0.5;

    /// <summary>
    ///     Maximum progress value (100%).
    /// </summary>
    private const double MaxProgress = 100;

    /// <summary>
    ///     Default timeout for operations in milliseconds.
    /// </summary>
    private const int OperationTimeoutMs = 5000;

    /// <summary>
    ///     State lock for thread-safe operations.
    ///     Uses .NET 9+ Lock type for better semantics and performance.
    /// </summary>
    private readonly Lock _stateLock = new();

    /// <summary>
    ///     Timer for automated progress updates.
    /// </summary>
    private readonly Timer _progressTimer;

    /// <summary>
    ///     Logger for diagnostic information.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    ///     Thread-safe dictionary of step states.
    /// </summary>
    private readonly ConcurrentDictionary<string, StepState> _stepStates;

    /// <summary>
    ///     Thread-safe dictionary of task progress values.
    /// </summary>
    private readonly ConcurrentDictionary<string, double> _taskProgress;

    /// <summary>
    ///     Lock for thread-safe updates.
    /// </summary>
    private readonly SemaphoreSlim _updateLock;

    /// <summary>
    ///     Cancellation token source for controlled shutdown.
    /// </summary>
    private readonly CancellationTokenSource _disposalCts;

    /// <summary>
    ///     Cached step states list to reduce allocations.
    /// </summary>
    private List<StepState>? _cachedStepStates;

    /// <summary>
    ///     Flag to track if the cached step states are valid.
    /// </summary>
    private bool _stepStatesChanged = true;

    /// <summary>
    ///     Flag to track disposal state.
    /// </summary>
    private int _isDisposed;

    /// <summary>
    ///     Flag indicating if indeterminate mode is active.
    /// </summary>
    private volatile bool _isIndeterminate;

    /// <summary>
    ///     Current progress value (0-100).
    /// </summary>
    private double _progress;

#if !NET10_0_OR_GREATER
    /// <summary>
    ///     Current progress message (backing field for .NET 9).
    /// </summary>
    private string? _message = string.Empty;
#endif

    #endregion

    #region Public Properties

    /// <summary>
    ///     Gets whether this manager has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _isDisposed) == 1;

    /// <summary>
    ///     Gets the current step states with minimal allocations.
    /// </summary>
    public IReadOnlyList<StepState> CurrentStepStates
    {
        get
        {
            if (_stepStatesChanged || _cachedStepStates == null)
            {
                lock (_stateLock)
                {
                    _cachedStepStates = _stepStates.Values.ToList();
                    _stepStatesChanged = false;
                }
            }

            return _cachedStepStates;
        }
    }

    /// <summary>
    ///     Gets the current progress message.
    /// </summary>
#if NET10_0_OR_GREATER
    public string? Message { get => field; private set => field = value ?? string.Empty; } = string.Empty;
#else
    public string? Message { get => _message; private set => _message = value ?? string.Empty; }
#endif

    /// <summary>
    ///     Gets the current progress value (0-100).
    /// </summary>
    public double Progress => Volatile.Read(ref _progress);

    /// <summary>
    ///     Gets whether the progress is in indeterminate mode.
    /// </summary>
    public bool IsIndeterminate => _isIndeterminate;

    /// <summary>
    ///     Gets the step configurations, if any.
    /// </summary>
    public IReadOnlyList<ProgressStepConfig>? Steps { get; private set; }

    /// <summary>
    ///     Gets the cancellation token for progress operations.
    /// </summary>
    public CancellationToken CancellationToken => _disposalCts.Token;

    #endregion

    #region Public Methods

    /// <summary>
    ///     Starts an indeterminate progress indicator.
    /// </summary>
    /// <param name="message">Progress message.</param>
    public void StartIndeterminate(string? message)
    {
        ThrowIfDisposed();
        lock (_stateLock)
        {
            Reset();
            _isIndeterminate = true;
            Message = message;
            NotifyStateChangedAsync().FireAndForget(_logger);
        }
    }

    /// <summary>
    ///     Starts an indeterminate progress indicator with Result pattern support.
    /// </summary>
    /// <param name="message">Progress message.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result<Unit, ProgressManagerError> StartIndeterminateWithResult(string? message)
    {
        try
        {
            ThrowIfDisposed();
            lock (_stateLock)
            {
                Reset();
                _isIndeterminate = true;
                Message = message;
                NotifyStateChangedAsync().FireAndForget(_logger);
            }

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start indeterminate progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to start indeterminate progress", ex));
        }
    }

    /// <summary>
    ///     Starts a determinate progress task.
    /// </summary>
    /// <param name="message">Progress message.</param>
    public void StartTask(string? message)
    {
        ThrowIfDisposed();
        lock (_stateLock)
        {
            Reset();
            _isIndeterminate = false;
            Message = message;
            StartTimer();
            NotifyStateChangedAsync().FireAndForget(_logger);
        }
    }

    /// <summary>
    ///     Starts a determinate progress task with Result pattern support.
    /// </summary>
    /// <param name="message">Progress message.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result<Unit, ProgressManagerError> StartTaskWithResult(string? message)
    {
        try
        {
            ThrowIfDisposed();
            lock (_stateLock)
            {
                Reset();
                _isIndeterminate = false;
                Message = message;
                StartTimer();
                NotifyStateChangedAsync().FireAndForget(_logger);
            }

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start task progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to start task progress", ex));
        }
    }

    /// <summary>
    ///     Starts a stepped progress indicator.
    /// </summary>
    /// <param name="steps">Step configurations.</param>
    public void StartSteps(List<ProgressStepConfig> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0)
        {
            throw new ArgumentException("Steps cannot be empty", nameof(steps));
        }

        ThrowIfDisposed();
        lock (_stateLock)
        {
            Reset();
            Steps = steps;

            foreach (var step in steps)
            {
                var state = new StepState(step.Id, step.Name, step.Tooltip ?? string.Empty);
                state.OnStateChanged += HandleStepStateChanged;
                _stepStates[step.Id] = state;
            }

            // Mark step states as changed
            _stepStatesChanged = true;
            UpdateOverallProgress();
            NotifyStateChangedAsync().FireAndForget(_logger);
        }
    }

    /// <summary>
    ///     Starts a stepped progress indicator with Result pattern support.
    /// </summary>
    /// <param name="steps">Step configurations.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result<Unit, ProgressManagerError> StartStepsWithResult(IReadOnlyList<ProgressStepConfig> steps)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(steps);
            if (steps.Count == 0)
            {
                return Result<Unit, ProgressManagerError>.Failure(
                    new ProgressManagerError("Steps cannot be empty"));
            }

            ThrowIfDisposed();
            lock (_stateLock)
            {
                Reset();
                Steps = steps;

                foreach (var step in steps)
                {
                    var state = new StepState(step.Id, step.Name, step.Tooltip ?? string.Empty);
                    state.OnStateChanged += HandleStepStateChanged;
                    _stepStates[step.Id] = state;
                }

                // Mark step states as changed
                _stepStatesChanged = true;
                UpdateOverallProgress();
                NotifyStateChangedAsync().FireAndForget(_logger);
            }

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start stepped progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to start stepped progress", ex));
        }
    }

    /// <summary>
    ///     Marks progress as complete.
    /// </summary>
    public void Complete()
    {
        ThrowIfDisposed();
        lock (_stateLock)
        {
            StopTimer();
            Volatile.Write(ref _progress, MaxProgress);
            Message = "Completed";
            NotifyStateChangedAsync().FireAndForget(_logger);
        }
    }

    /// <summary>
    ///     Marks progress as complete with Result pattern support.
    /// </summary>
    /// <returns>A Result indicating success or failure.</returns>
    public Result<Unit, ProgressManagerError> CompleteWithResult()
    {
        try
        {
            ThrowIfDisposed();
            lock (_stateLock)
            {
                StopTimer();
                Volatile.Write(ref _progress, MaxProgress);
                Message = "Completed";
                NotifyStateChangedAsync().FireAndForget(_logger);
            }

            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to complete progress");
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to complete progress", ex));
        }
    }

    /// <summary>
    ///     Updates progress with thread-safe state management.
    /// </summary>
    /// <param name="taskId">Task/step identifier.</param>
    /// <param name="progress">Progress value (0-100).</param>
    /// <param name="status">Step status.</param>
    /// <param name="message">Optional message update.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task UpdateProgressAsync(
        string taskId,
        double progress,
        StepStatus status,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ThrowIfDisposed();

        // Use a linked token that integrates with disposal
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken);

        if (_stepStates.TryGetValue(taskId, out var step))
        {
            step.UpdateProgress(progress, status);
            if (!string.IsNullOrEmpty(message))
            {
                Message = message;
            }

            return;
        }

        await UpdateTaskProgressAsync(taskId, progress, message, linkedCts.Token);
    }

    /// <summary>
    ///     Updates progress with Result pattern support.
    /// </summary>
    /// <param name="taskId">Task/step identifier.</param>
    /// <param name="progress">Progress value (0-100).</param>
    /// <param name="status">Step status.</param>
    /// <param name="message">Optional message update.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<Unit, ProgressManagerError>> UpdateProgressWithResultAsync(
        string taskId,
        double progress,
        StepStatus status,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
            ThrowIfDisposed();

            // Use a linked token that integrates with disposal
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _disposalCts.Token,
                cancellationToken);

            if (_stepStates.TryGetValue(taskId, out var step))
            {
                step.UpdateProgress(progress, status);
                if (!string.IsNullOrEmpty(message))
                {
                    Message = message;
                }

                return Result<Unit, ProgressManagerError>.Success(Unit.Value);
            }

            await UpdateTaskProgressAsync(taskId, progress, message, linkedCts.Token);
            return Result<Unit, ProgressManagerError>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update progress for task {TaskId}", taskId);
            return Result<Unit, ProgressManagerError>.Failure(
                new ProgressManagerError($"Failed to update progress for task {taskId}", ex));
        }
    }

    /// <summary>
    ///     Gets service metrics for monitoring.
    /// </summary>
    /// <returns>A Result with performance metrics.</returns>
    public Result<IDictionary<string, object>, ProgressManagerError> GetServiceMetrics()
    {
        try
        {
            ThrowIfDisposed();

            var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["Progress"] = Progress,
                ["IsIndeterminate"] = IsIndeterminate,
                ["StepCount"] = _stepStates.Count,
                ["TaskCount"] = _taskProgress.Count,
                ["HasSteps"] = Steps != null,
                ["IsDisposed"] = IsDisposed,
                ["Timestamp"] = DateTime.UtcNow
            };

            return Result<IDictionary<string, object>, ProgressManagerError>.Success(metrics);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get service metrics");
            return Result<IDictionary<string, object>, ProgressManagerError>.Failure(
                new ProgressManagerError("Failed to get service metrics", ex));
        }
    }

    /// <summary>
    ///     Asynchronously disposes the manager.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            // Signal cancellation for all pending operations
            await _disposalCts.CancelAsync();

            // Stop the progress timer
            StopTimer();

            // Unsubscribe from step events
            foreach (var step in _stepStates.Values)
            {
                step.OnStateChanged -= HandleStepStateChanged;
            }

            // Clear collections
            _stepStates.Clear();
            _taskProgress.Clear();
            _cachedStepStates = null;
            Steps = null;

            // Dispose resources
            _progressTimer.Dispose();
            _updateLock.Dispose();
            _disposalCts.Dispose();

            _logger.Debug("Progress manager disposed");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during progress manager disposal");
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Updates task progress asynchronously.
    /// </summary>
    /// <param name="taskId">Task identifier.</param>
    /// <param name="progress">Progress value (0-100).</param>
    /// <param name="message">Optional message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task UpdateTaskProgressAsync(
        string taskId,
        double progress,
        string? message,
        CancellationToken cancellationToken)
    {
        if (progress is < 0 or > MaxProgress)
        {
            throw new ArgumentOutOfRangeException(
                nameof(progress),
                "Progress must be between 0 and 100"
            );
        }

        await _updateLock.WaitAsync(cancellationToken);
        try
        {
            _taskProgress[taskId] = progress;
            var newProgress = _taskProgress.Values.DefaultIfEmpty(0).Average();
            Volatile.Write(ref _progress, newProgress);

            if (!string.IsNullOrEmpty(message))
            {
                Message = message;
            }

            await NotifyStateChangedAsync();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    ///     Starts the progress timer.
    /// </summary>
    private void StartTimer()
    {
        StopTimer(); // Ensure timer is stopped first
        _progressTimer.Change(0, (int)DefaultTimerIntervalMs);
    }

    /// <summary>
    ///     Handles the timer callback.
    /// </summary>
    private void UpdateProgress(object? state)
    {
        if (IsDisposed)
        {
            return;
        }

        IncrementProgressAsync(DefaultProgressIncrement).FireAndForget(_logger);
    }

    /// <summary>
    ///     Stops the progress timer.
    /// </summary>
    private void StopTimer()
    {
        _progressTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    ///     Increments progress by the specified amount.
    /// </summary>
    /// <param name="amount">Amount to increment by.</param>
    private async Task IncrementProgressAsync(double amount)
    {
        try
        {
            await _updateLock.WaitAsync(_disposalCts.Token);
            try
            {
                var currentProgress = Progress;
                var newProgress = Math.Min(currentProgress + amount, MaxProgress);

                if (Math.Abs(newProgress - currentProgress) > 0.01)
                {
                    Volatile.Write(ref _progress, newProgress);
                    await NotifyStateChangedAsync();

                    if (newProgress >= MaxProgress)
                    {
                        StopTimer();
                        Complete();
                    }
                }
            }
            finally
            {
                _updateLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error incrementing progress");
        }
    }

    /// <summary>
    ///     Resets the progress state.
    /// </summary>
    private void Reset()
    {
        StopTimer();
        _taskProgress.Clear();
        Volatile.Write(ref _progress, 0);
        Message = string.Empty;
        _isIndeterminate = false;

        foreach (var step in _stepStates.Values)
        {
            step.OnStateChanged -= HandleStepStateChanged;
        }

        _stepStates.Clear();
        _cachedStepStates = null;
        _stepStatesChanged = true;
        Steps = null;
    }

    /// <summary>
    ///     Notifies subscribers of state changes.
    /// </summary>
    private async Task NotifyStateChangedAsync()
    {
        if (StateChanged != null)
        {
            await StateChanged.Invoke();
        }
    }

    /// <summary>
    ///     Handles step state changes.
    /// </summary>
    /// <param name="step">The step that changed.</param>
    private void HandleStepStateChanged(StepState step)
    {
        _stepStatesChanged = true;
        UpdateOverallProgress();
        NotifyStateChangedAsync().FireAndForget(_logger);
    }

    /// <summary>
    ///     Updates the overall progress based on steps or task progress.
    /// </summary>
    private void UpdateOverallProgress()
    {
        var newProgress = !_stepStates.IsEmpty
            ? _stepStates.Values.Average(s => s.Progress)
            : _taskProgress.Values.DefaultIfEmpty(0).Average();

        Volatile.Write(ref _progress, newProgress);
    }

    /// <summary>
    ///     Throws an ObjectDisposedException if the service is disposed.
    /// </summary>
    /// <param name="caller">Name of the calling method.</param>
    private void ThrowIfDisposed([CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(
                GetType().Name,
                $"Cannot {caller} on disposed ProgressManager"
            );
        }
    }

    #endregion
}

/// <summary>
///     Provides a fire-and-forget pattern for tasks with error handling.
/// </summary>
internal static class TaskExtensions
{
    /// <summary>
    ///     Executes a task without awaiting, but logs any errors.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="logger">The logger for error handling.</param>
    public static void FireAndForget(this Task task, ILogger logger)
    {
        _ = task.ContinueWith(
            t => logger.Error(t.Exception!, "Unhandled task error"),
            TaskContinuationOptions.OnlyOnFaulted
        );
    }
}
