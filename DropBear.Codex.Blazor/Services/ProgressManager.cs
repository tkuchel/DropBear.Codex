#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Timers;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Serilog;
using Timer = System.Timers.Timer;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Provides progress tracking for various types of operations, including:
///     - Indeterminate progress (spinners)
///     - Single-task determinate progress
///     - Step-based progress with multiple steps.
/// </summary>
public class ProgressManager : IProgressManager
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ILogger _logger;
    private readonly Timer _progressTimer;
    private readonly ConcurrentDictionary<string, StepState> _stepStates = new();
    private readonly ConcurrentDictionary<string, double> _taskProgress = new();
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProgressManager" /> class.
    /// </summary>
    public ProgressManager()
    {
        // Timer used to increment progress in single-task mode if needed
        _progressTimer = new Timer(100) { AutoReset = true, Enabled = false };
        _progressTimer.Elapsed += OnTimerElapsed;
        IsDisposed = false;
        _logger = LoggerFactory.Logger.ForContext<ProgressManager>();
        _logger.Debug("ProgressManager instance created.");
    }

    /// <summary>
    ///     Indicates whether this instance has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    ///     Occurs when the state changes, allowing consumers to update the UI.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    ///     Gets the current list of step states, if running in step-based mode.
    /// </summary>
    public IReadOnlyList<StepState> CurrentStepStates => _stepStates.Values.ToList();

    /// <summary>
    ///     Gets the current progress message.
    /// </summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>
    ///     Gets the overall progress percentage (0-100).
    /// </summary>
    public double Progress { get; private set; }

    /// <summary>
    ///     Indicates whether the progress is in indeterminate mode.
    /// </summary>
    public bool IsIndeterminate { get; private set; }

    /// <summary>
    ///     Gets the step configurations, if running in stepped mode.
    /// </summary>
    public IReadOnlyList<ProgressStepConfig>? Steps { get; private set; }

    /// <summary>
    ///     Provides a cancellation token that signals when this progress instance is disposed.
    /// </summary>
    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    /// <summary>
    ///     Disposes the <see cref="ProgressManager" /> instance and its resources.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        StopTimer();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _updateLock.Dispose();
        _progressTimer.Dispose();

        // Unsubscribe from step OnStateChanged events
        foreach (var step in _stepStates.Values)
        {
            step.OnStateChanged -= HandleStepStateChanged;
        }

        _logger.Debug("ProgressManager instance disposed.");
    }

    /// <summary>
    ///     Starts an indeterminate progress operation (e.g., spinner).
    /// </summary>
    /// <param name="message">The user-facing message describing the operation.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public void StartIndeterminate(string message)
    {
        ValidateNotDisposed();
        Reset();
        IsIndeterminate = true;
        Message = message;
        NotifyStateChanged();
    }

    /// <summary>
    ///     Starts a single-task progress operation, enabling a timer-based increment if desired.
    /// </summary>
    /// <param name="message">The user-facing message describing the operation.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public void StartTask(string message)
    {
        ValidateNotDisposed();
        Reset();
        IsIndeterminate = false;
        Message = message;
        _progressTimer.Start(); // Timer increments progress automatically
        NotifyStateChanged();
    }

    /// <summary>
    ///     Starts a step-based progress operation with a list of step configurations.
    /// </summary>
    /// <param name="steps">The list of step configurations.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public void StartSteps(List<ProgressStepConfig> steps)
    {
        ValidateNotDisposed();
        if (steps is null || steps.Count == 0)
        {
            throw new ArgumentException("Steps cannot be null or empty.", nameof(steps));
        }

        Reset();
        Steps = steps;

        // Create step states and subscribe to changes
        foreach (var step in steps)
        {
            var stepState = new StepState(step.Id, step.Name, step.Tooltip ?? string.Empty);
            stepState.OnStateChanged += HandleStepStateChanged;
            _stepStates[step.Id] = stepState;
        }

        // Initialize overall progress
        UpdateOverallProgress();
        NotifyStateChanged();
    }

    /// <summary>
    ///     Marks the current operation as complete, setting progress to 100%.
    /// </summary>
    public void Complete()
    {
        StopTimer();
        Progress = 100;
        Message = "Completed";
        NotifyStateChanged();
    }

    /// <summary>
    ///     Updates progress for either a single task or a step, depending on whether a step with <paramref name="taskId" />
    ///     exists.
    /// </summary>
    /// <param name="taskId">The identifier for the task or step.</param>
    /// <param name="progress">The progress percentage (0-100).</param>
    /// <param name="status">The status of the step, if applicable.</param>
    /// <param name="message">An optional message to override the current progress message.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public async Task UpdateProgressAsync(string taskId, double progress, StepStatus status, string? message = null)
    {
        ValidateNotDisposed();
        if (string.IsNullOrEmpty(taskId))
        {
            throw new ArgumentException("Task ID cannot be null or empty.", nameof(taskId));
        }

        // Check if this is a step update
        if (_stepStates.TryGetValue(taskId, out var stepState))
        {
            // It's a step-based progress update
            stepState.UpdateProgress(progress, status);

            // Optionally update the main message
            if (!string.IsNullOrEmpty(message))
            {
                Message = message;
            }
        }
        else
        {
            // It's a single-task update
            if (progress < 0 || progress > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100.");
            }

            // Lock to update concurrent dictionary safely
            await _updateLock.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                _taskProgress[taskId] = progress;
                Progress = _taskProgress.Values.DefaultIfEmpty(0).Average();

                if (!string.IsNullOrEmpty(message))
                {
                    Message = message;
                }

                NotifyStateChanged();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating progress: {ex.Message}");
                _logger.Error(ex, "Error updating single task progress.");
            }
            finally
            {
                _updateLock.Release();
            }
        }
    }

    /// <summary>
    ///     Resets the progress manager state, stopping timers, clearing progress, and removing steps.
    /// </summary>
    private void Reset()
    {
        StopTimer();
        _taskProgress.Clear();
        Progress = 0;
        Message = string.Empty;
        IsIndeterminate = false;

        // Unsubscribe from step events and clear
        if (_stepStates.Any())
        {
            foreach (var step in _stepStates.Values)
            {
                step.OnStateChanged -= HandleStepStateChanged;
            }

            _stepStates.Clear();
        }

        Steps = null;
        NotifyStateChanged();
    }

    /// <summary>
    ///     Stops the internal progress timer.
    /// </summary>
    private void StopTimer()
    {
        _progressTimer.Stop();
    }

    /// <summary>
    ///     Handles the timer's elapsed event to increment progress automatically.
    /// </summary>
    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            // Micro-optimization: store increment in a constant if desired
            const double timerIncrement = 0.5;
            await IncrementProgressAsync(timerIncrement).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Timer error: {ex.Message}");
            _logger.Error(ex, "Timer elapsed error.");
        }
    }

    /// <summary>
    ///     Increments the current progress by a specified amount (up to 100).
    /// </summary>
    private async Task IncrementProgressAsync(double amount)
    {
        await _updateLock.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            Progress = Math.Min(Progress + amount, 100);
            NotifyStateChanged();

            // If we reached 100%, stop timer and call complete
            if (Progress >= 100)
            {
                StopTimer();
                Complete();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error incrementing progress: {ex.Message}");
            _logger.Error(ex, "Error incrementing progress.");
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    ///     Raises the <see cref="StateChanged" /> event.
    /// </summary>
    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    /// <summary>
    ///     Ensures this instance is not disposed before proceeding.
    /// </summary>
    private void ValidateNotDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ProgressManager));
        }
    }

    /// <summary>
    ///     Handles state changes from individual steps by recalculating overall progress.
    /// </summary>
    private void HandleStepStateChanged(StepState stepState)
    {
        UpdateOverallProgress();
        NotifyStateChanged();
    }

    /// <summary>
    ///     Recalculates overall progress as the average of all step progress or single-task progress.
    /// </summary>
    private void UpdateOverallProgress()
    {
        Progress = !_stepStates.IsEmpty ? _stepStates.Values.Average(s => s.Progress) : _taskProgress.Values.DefaultIfEmpty(0).Average();
    }
}
