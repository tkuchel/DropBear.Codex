#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Thread-safe state tracking for a single progress step.
///     Optimized for use in Blazor Server.
/// </summary>
public class StepProgressState : IAsyncDisposable
{
    private const double MinProgress = 0;
    private const double MaxProgress = 100;
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private volatile bool _hasStarted;

    private int _isDisposed;
    private double _progress;
    private DateTimeOffset _startTime;
    private volatile StepStatus _status;

    public StepProgressState()
    {
        _startTime = DateTimeOffset.UtcNow;
        LastUpdateTime = _startTime;
    }

    /// <summary>
    ///     Gets the current progress (0-100).
    /// </summary>
    public double Progress => Volatile.Read(ref _progress);

    /// <summary>
    ///     Gets the current step status.
    /// </summary>
    public StepStatus Status => _status;

    /// <summary>
    ///     Gets the last update timestamp.
    /// </summary>
    public DateTimeOffset LastUpdateTime { get; private set; }

    /// <summary>
    ///     Gets whether this state has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _isDisposed) == 1;

    /// <summary>
    ///     Gets the active running duration.
    /// </summary>
    public TimeSpan RunningTime => _hasStarted
        ? DateTimeOffset.UtcNow - _startTime
        : TimeSpan.Zero;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _disposalCts.CancelAsync().ConfigureAwait(false);
            await _updateLock.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            try
            {
                _progress = 0;
                _status = StepStatus.NotStarted;
                _hasStarted = false;
            }
            finally
            {
                _updateLock.Release();
                _updateLock.Dispose();
                _disposalCts.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disposing StepProgressState: {ex}");
        }
    }

    /// <summary>
    ///     Updates progress and status with thread safety.
    /// </summary>
    /// <param name="newProgress">Progress value (0-100).</param>
    /// <param name="newStatus">Step status.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task UpdateProgressAsync(
        double newProgress,
        StepStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );

        await _updateLock.WaitAsync(cts.Token).ConfigureAwait(false);
        try
        {
            Volatile.Write(ref _progress, Math.Clamp(newProgress, MinProgress, MaxProgress));

            var previousStatus = _status;
            _status = newStatus;

            if (newStatus == StepStatus.InProgress && !_hasStarted)
            {
                _hasStarted = true;
                _startTime = DateTimeOffset.UtcNow;
            }

            LastUpdateTime = DateTimeOffset.UtcNow;

            if (previousStatus != newStatus)
            {
                await OnStatusChanged(previousStatus, newStatus).ConfigureAwait(false);
            }
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    ///     Sets progress directly with thread safety.
    /// </summary>
    public void SetProgress(double newProgress)
    {
        ThrowIfDisposed();
        Volatile.Write(
            ref _progress,
            Math.Clamp(newProgress, MinProgress, MaxProgress)
        );
    }

    /// <summary>
    ///     Sets status directly with thread safety.
    /// </summary>
    public void SetStatus(StepStatus newStatus)
    {
        ThrowIfDisposed();
        var previousStatus = _status;
        _status = newStatus;

        if (newStatus == StepStatus.InProgress && !_hasStarted)
        {
            _hasStarted = true;
            _startTime = DateTimeOffset.UtcNow;
        }

        _ = OnStatusChanged(previousStatus, newStatus)
            .ContinueWith(
                t => Console.WriteLine($"Status change error: {t.Exception}"),
                TaskContinuationOptions.OnlyOnFaulted
            );
    }

    /// <summary>
    ///     Resets the state to initial values.
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );

        await _updateLock.WaitAsync(cts.Token).ConfigureAwait(false);
        try
        {
            Volatile.Write(ref _progress, MinProgress);
            _status = StepStatus.NotStarted;
            _hasStarted = false;
            _startTime = DateTimeOffset.UtcNow;
            LastUpdateTime = _startTime;
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    ///     Override to handle status changes.
    /// </summary>
    protected virtual Task OnStatusChanged(StepStatus oldStatus, StepStatus newStatus)
    {
        return Task.CompletedTask;
    }

    private void ThrowIfDisposed([CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(
                GetType().Name,
                $"Cannot {caller} on disposed StepProgressState"
            );
        }
    }
}
