#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

#endregion

namespace DropBear.Codex.Blazor.Models;

public sealed class ProgressState : IAsyncDisposable
{
    private const int DefaultCapacity = 8;
    private const double MinProgress = 0;
    private const double MaxProgress = 100;
    private readonly CancellationTokenSource _disposalCts;

    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly ConcurrentDictionary<string, StepProgressState> _stepStates;

    private volatile string? _currentMessage;
    private int _isDisposed;
    private volatile bool _isIndeterminate;
    private double _overallProgress;

    public ProgressState(int? capacity = null)
    {
        _stepStates = new ConcurrentDictionary<string, StepProgressState>(
            Environment.ProcessorCount,
            capacity ?? DefaultCapacity
        );
        _disposalCts = new CancellationTokenSource();
        StartTime = DateTime.UtcNow;
    }

    public bool IsIndeterminate => _isIndeterminate;
    public double OverallProgress => Volatile.Read(ref _overallProgress);
    public string? CurrentMessage => _currentMessage;
    public DateTime StartTime { get; }
    public bool IsDisposed => Volatile.Read(ref _isDisposed) == 1;
    public IReadOnlyDictionary<string, StepProgressState> StepStates => _stepStates;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _disposalCts.CancelAsync().ConfigureAwait(false);

            await _stateLock.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            try
            {
                // Properly dispose each StepProgressState
                foreach (var state in _stepStates.Values)
                {
                    try
                    {
                        await state.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // Continue disposing other states
                    }
                }

                _stepStates.Clear();
            }
            finally
            {
                _stateLock.Dispose();
            }
        }
        finally
        {
            _disposalCts.Dispose();
        }
    }

    public async Task SetIndeterminateAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );

        await _stateLock.WaitAsync(cts.Token).ConfigureAwait(false);
        try
        {
            // Dispose existing step states before clearing
            foreach (var state in _stepStates.Values)
            {
                await state.DisposeAsync().ConfigureAwait(false);
            }

            _isIndeterminate = true;
            _currentMessage = message;
            Volatile.Write(ref _overallProgress, MinProgress);
            _stepStates.Clear();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task UpdateOverallProgressAsync(
        double progress,
        string message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );

        await _stateLock.WaitAsync(cts.Token).ConfigureAwait(false);
        try
        {
            _isIndeterminate = false;
            Volatile.Write(ref _overallProgress, ClampProgress(progress));
            _currentMessage = message;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public StepProgressState GetOrCreateStepState(string stepId)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(stepId);

        return _stepStates.GetOrAdd(stepId, static _ => new StepProgressState());
    }

    public bool TryRemoveStepState(string stepId)
    {
        ThrowIfDisposed();

        if (_stepStates.TryRemove(stepId, out var state))
        {
            // Dispose the removed state
            _ = state.DisposeAsync()
                .AsTask()
                .ContinueWith(
                    t => Console.WriteLine($"Error disposing step state: {t.Exception}"),
                    TaskContinuationOptions.OnlyOnFaulted
                );
            return true;
        }

        return false;
    }

    public bool TryGetStepState(string stepId, out StepProgressState? state)
    {
        ThrowIfDisposed();
        return _stepStates.TryGetValue(stepId, out state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ClampProgress(double value)
    {
        return Math.Clamp(value, MinProgress, MaxProgress);
    }

    private void ThrowIfDisposed([CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(
                GetType().Name,
                $"Cannot {caller} on disposed ProgressState"
            );
        }
    }
}
