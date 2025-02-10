#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Thread-safe progress tracking manager optimized for Blazor Server.
/// </summary>
public sealed class ProgressManager : IProgressManager
{
    private const double DefaultTimerIntervalMs = 100;
    private const double DefaultProgressIncrement = 0.5;
    private const double MaxProgress = 100;
    private const int OperationTimeoutMs = 5000;
    private readonly CancellationTokenSource _disposalCts;

    private readonly ILogger _logger;
    private readonly PeriodicTimer _progressTimer;
    private readonly object _stateLock = new();
    private readonly ConcurrentDictionary<string, StepState> _stepStates;
    private readonly ConcurrentDictionary<string, double> _taskProgress;
    private readonly SemaphoreSlim _updateLock;

    private int _isDisposed;
    private volatile bool _isIndeterminate;
    private string _message = string.Empty;
    private double _progress;
    private Task? _timerTask;

    public ProgressManager()
    {
        _logger = LoggerFactory.Logger.ForContext<ProgressManager>();
        _disposalCts = new CancellationTokenSource();
        _stepStates = new ConcurrentDictionary<string, StepState>();
        _taskProgress = new ConcurrentDictionary<string, double>();
        _updateLock = new SemaphoreSlim(1, 1);
        _progressTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(DefaultTimerIntervalMs));

        _logger.Debug("Progress manager initialized");
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _disposalCts.CancelAsync();
            await StopTimerAsync();

            foreach (var step in _stepStates.Values)
            {
                step.OnStateChanged -= HandleStepStateChanged;
            }

            _stepStates.Clear();
            _taskProgress.Clear();
            Steps = null;
        }
        finally
        {
            _disposalCts.Dispose();
            _updateLock.Dispose();
            _progressTimer.Dispose();
            _logger.Debug("Progress manager disposed");
        }
    }

    public bool IsDisposed => Volatile.Read(ref _isDisposed) == 1;
    public IReadOnlyList<StepState> CurrentStepStates => _stepStates.Values.ToList();
    public string Message { get => _message; private set => _message = value ?? string.Empty; }
    public double Progress => Volatile.Read(ref _progress);
    public bool IsIndeterminate => _isIndeterminate;
    public IReadOnlyList<ProgressStepConfig>? Steps { get; private set; }

    public CancellationToken CancellationToken => _disposalCts.Token;

    public void StartIndeterminate(string message)
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

    public void StartTask(string message)
    {
        ThrowIfDisposed();
        lock (_stateLock)
        {
            Reset();
            _isIndeterminate = false;
            Message = message;
            StartTimerAsync().FireAndForget(_logger);
            NotifyStateChangedAsync().FireAndForget(_logger);
        }
    }

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

            UpdateOverallProgress();
            NotifyStateChangedAsync().FireAndForget(_logger);
        }
    }

    public void Complete()
    {
        ThrowIfDisposed();
        lock (_stateLock)
        {
            StopTimerAsync().FireAndForget(_logger);
            Volatile.Write(ref _progress, MaxProgress);
            Message = "Completed";
            NotifyStateChangedAsync().FireAndForget(_logger);
        }
    }

    public event Func<Task>? StateChanged;

    public async Task UpdateProgressAsync(
        string taskId,
        double progress,
        StepStatus status,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ThrowIfDisposed();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );
        cts.CancelAfter(OperationTimeoutMs);

        if (_stepStates.TryGetValue(taskId, out var step))
        {
            step.UpdateProgress(progress, status);
            if (!string.IsNullOrEmpty(message))
            {
                Message = message;
            }

            return;
        }

        await UpdateTaskProgressAsync(taskId, progress, message, cts.Token);
    }

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

    private async Task StartTimerAsync()
    {
        await StopTimerAsync();
        _timerTask = RunTimerAsync();
    }

    private async Task RunTimerAsync()
    {
        try
        {
            while (await _progressTimer.WaitForNextTickAsync(_disposalCts.Token))
            {
                await IncrementProgressAsync(DefaultProgressIncrement);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Timer error");
        }
    }

    private async Task StopTimerAsync()
    {
        if (_timerTask == null)
        {
            return;
        }

        try
        {
            await _timerTask;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error(ex, "Error stopping timer");
        }
        finally
        {
            _timerTask = null;
        }
    }

    private async Task IncrementProgressAsync(double amount)
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
                    await StopTimerAsync();
                    Complete();
                }
            }
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private void Reset()
    {
        StopTimerAsync().FireAndForget(_logger);
        _taskProgress.Clear();
        Volatile.Write(ref _progress, 0);
        Message = string.Empty;
        _isIndeterminate = false;

        foreach (var step in _stepStates.Values)
        {
            step.OnStateChanged -= HandleStepStateChanged;
        }

        _stepStates.Clear();
        Steps = null;
    }

    private async Task NotifyStateChangedAsync()
    {
        if (StateChanged != null)
        {
            await StateChanged.Invoke();
        }
    }

    private void HandleStepStateChanged(StepState step)
    {
        UpdateOverallProgress();
        NotifyStateChangedAsync().FireAndForget(_logger);
    }

    private void UpdateOverallProgress()
    {
        var newProgress = !_stepStates.IsEmpty
            ? _stepStates.Values.Average(s => s.Progress)
            : _taskProgress.Values.DefaultIfEmpty(0).Average();

        Volatile.Write(ref _progress, newProgress);
    }

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
}

internal static class TaskExtensions
{
    public static void FireAndForget(this Task task, ILogger logger)
    {
        task.ContinueWith(
            t => logger.Error(t.Exception!, "Unhandled task error"),
            TaskContinuationOptions.OnlyOnFaulted
        );
    }
}
