#region

using System.Collections.Concurrent;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Thread-safe progress state manager for the progress bar
/// </summary>
public sealed class ProgressState : IAsyncDisposable
{
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly ConcurrentDictionary<string, StepProgressState> _stepStates = new();
    private volatile string? _currentMessage;
    private volatile bool _isDisposed;
    private volatile bool _isIndeterminate;

    /// <summary>
    ///     Gets whether the progress is in indeterminate mode
    /// </summary>
    public bool IsIndeterminate => _isIndeterminate;

    /// <summary>
    ///     Gets the current overall progress (0-100)
    /// </summary>
    public double OverallProgress { get; private set; }

    /// <summary>
    ///     Gets the current status message
    /// </summary>
    public string? CurrentMessage => _currentMessage;

    /// <summary>
    ///     Gets a read-only view of step states
    /// </summary>
    public IReadOnlyDictionary<string, StepProgressState> StepStates => _stepStates;

    public DateTime StartTime { get; } = DateTime.UtcNow;

    /// <summary>
    ///     Disposes of resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var state in _stepStates.Values)
            {
                await state.UpdateLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    state.UpdateLock.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }

            _stepStates.Clear();
            _stateLock.Dispose();
        }
        catch (Exception)
        {
            // Ignore disposal errors
        }
    }

    /// <summary>
    ///     Sets the progress bar to indeterminate mode
    /// </summary>
    public async Task SetIndeterminateAsync(string message)
    {
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _isIndeterminate = true;
            _currentMessage = message;
            OverallProgress = 0;
            _stepStates.Clear();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    ///     Updates the overall progress and message
    /// </summary>
    public async Task UpdateOverallProgressAsync(double progress, string message)
    {
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _isIndeterminate = false;
            OverallProgress = Math.Clamp(progress, 0, 100);
            _currentMessage = message;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    ///     Gets or creates a step state
    /// </summary>
    public StepProgressState GetOrCreateStepState(string stepId)
    {
        return _stepStates.GetOrAdd(stepId, _ => new StepProgressState());
    }
}
