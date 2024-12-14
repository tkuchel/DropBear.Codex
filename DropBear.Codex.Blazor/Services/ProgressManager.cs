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
///     Provides progress tracking for various types of operations, including indeterminate,
///     single-task, and step-based progress.
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
        _progressTimer = new Timer(100) { AutoReset = true, Enabled = false };
        _progressTimer.Elapsed += OnTimerElapsed;
        IsDisposed = false;
        _logger = LoggerFactory.Logger.ForContext<ProgressManager>();
        _logger.Debug("ProgressManager instance created.");
    }

    /// <summary>
    ///     Gets a value indicating whether the instance has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    ///     Gets the current step states.
    /// </summary>
    public IReadOnlyList<StepState> CurrentStepStates => _stepStates.Values.ToList();

    /// <summary>
    ///     Gets the current progress message.
    /// </summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>
    ///     Gets the overall progress percentage.
    /// </summary>
    public double Progress { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the progress is indeterminate.
    /// </summary>
    public bool IsIndeterminate { get; private set; }

    /// <summary>
    ///     Gets the step configurations, if any.
    /// </summary>
    public IReadOnlyList<ProgressStepConfig>? Steps { get; private set; }

    /// <summary>
    ///     Gets the cancellation token for progress operations.
    /// </summary>
    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    /// <summary>
    ///     Disposes of the <see cref="ProgressManager" /> instance and its resources.
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

        foreach (var step in _stepStates.Values)
        {
            step.OnStateChanged -= HandleStepStateChanged;
        }

        _logger.Debug("ProgressManager instance disposed.");
    }

    /// <summary>
    ///     Occurs when the state changes, allowing consumers to update the UI.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    ///     Starts an indeterminate progress operation.
    /// </summary>
    /// <param name="message">Message to display during the operation.</param>
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
    ///     Starts tracking a single task's progress.
    /// </summary>
    /// <param name="message">Message to display during the operation.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public void StartTask(string message)
    {
        ValidateNotDisposed();
        Reset();
        IsIndeterminate = false;
        Message = message;
        _progressTimer.Start();
        NotifyStateChanged();
    }

    /// <summary>
    ///     Starts a step-based progress operation.
    /// </summary>
    /// <param name="steps">Configurations for the steps.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public void StartSteps(List<ProgressStepConfig> steps)
    {
        ValidateNotDisposed();
        if (steps == null || steps.Count == 0)
        {
            throw new ArgumentException("Steps cannot be null or empty.", nameof(steps));
        }

        Reset();
        Steps = steps;

        foreach (var step in steps)
        {
            var stepState = new StepState(step.Id, step.Name, step.Tooltip ?? string.Empty);
            stepState.OnStateChanged += HandleStepStateChanged;
            _stepStates[step.Id] = stepState;
        }

        // Initialize overall progress based on steps
        UpdateOverallProgress();
        NotifyStateChanged();
    }

    /// <summary>
    ///     Marks the current progress operation as complete.
    /// </summary>
    public void Complete()
    {
        StopTimer();
        Progress = 100;
        Message = "Completed";
        NotifyStateChanged();
    }

    /// <summary>
    ///     Updates progress for a specific task or step.
    /// </summary>
    /// <param name="taskId">The ID of the task or step.</param>
    /// <param name="progress">The progress percentage (0-100).</param>
    /// <param name="status">The current status of the step.</param>
    /// <param name="message">An optional progress message.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public async Task UpdateProgressAsync(string taskId, double progress, StepStatus status, string? message = null)
    {
        ValidateNotDisposed();
        if (string.IsNullOrEmpty(taskId))
        {
            throw new ArgumentException("Task ID cannot be null or empty.", nameof(taskId));
        }

        // Determine if updating a step or a single task
        if (_stepStates.TryGetValue(taskId, out var stepState))
        {
            // It's a step
            stepState.UpdateProgress(progress, status);
            // Message update can be optional or related to the step
            if (!string.IsNullOrEmpty(message))
            {
                Message = message;
            }
        }
        else
        {
            // It's a single task
            if (progress < 0 || progress > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100.");
            }

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

    private void Reset()
    {
        StopTimer();
        _taskProgress.Clear();
        Progress = 0;
        Message = string.Empty;
        IsIndeterminate = false;

        // Reset steps if any
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

    private void StopTimer()
    {
        _progressTimer.Stop();
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            await IncrementProgressAsync(0.5).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Timer error: {ex.Message}");
            _logger.Error(ex, "Timer elapsed error.");
        }
    }

    private async Task IncrementProgressAsync(double amount)
    {
        await _updateLock.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            Progress = Math.Min(Progress + amount, 100);
            NotifyStateChanged();

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

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private void ValidateNotDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ProgressManager));
        }
    }

    private void HandleStepStateChanged(StepState stepState)
    {
        // Update overall progress based on step states
        UpdateOverallProgress();
        NotifyStateChanged();
    }

    private void UpdateOverallProgress()
    {
        Progress = !_stepStates.IsEmpty
            ? _stepStates.Values.Average(s => s.Progress)
            : _taskProgress.Values.DefaultIfEmpty(0).Average();
    }
}
