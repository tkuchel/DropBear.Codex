#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Timers;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
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
    private readonly Timer _progressTimer;
    private readonly ConcurrentDictionary<string, double> _taskProgress = new();
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private bool _isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProgressManager" /> class.
    /// </summary>
    public ProgressManager()
    {
        _progressTimer = new Timer(100) { AutoReset = true, Enabled = false };
        _progressTimer.Elapsed += OnTimerElapsed;
    }

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
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        StopTimer();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _updateLock.Dispose();
        _progressTimer.Dispose();
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
        Message = $"Step 1 of {steps.Count}";
        NotifyStateChanged();
    }

    /// <summary>
    ///     Updates progress for a specific task or step.
    /// </summary>
    /// <param name="taskId">The ID of the task or step.</param>
    /// <param name="progress">The progress percentage (0-100).</param>
    /// <param name="message">An optional progress message.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public async Task UpdateProgressAsync(string taskId, double progress, string? message = null)
    {
        ValidateNotDisposed();
        if (string.IsNullOrEmpty(taskId))
        {
            throw new ArgumentException("Task ID cannot be null or empty.", nameof(taskId));
        }

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
        }
        finally
        {
            _updateLock.Release();
        }
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

    private void Reset()
    {
        StopTimer();
        _taskProgress.Clear();
        Progress = 0;
        Message = string.Empty;
        IsIndeterminate = false;
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
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ProgressManager));
        }
    }
}
