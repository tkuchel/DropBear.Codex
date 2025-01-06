#region

using System.Collections.Concurrent;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Thread-safe progress state manager for a progress bar.
///     Manages overall progress, indeterminate states, and step-level states.
/// </summary>
public sealed class ProgressState : IAsyncDisposable
{
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly ConcurrentDictionary<string, StepProgressState> _stepStates = new();

    private volatile string? _currentMessage;
    private volatile bool _isDisposed;
    private volatile bool _isIndeterminate;

    /// <summary>
    ///     Gets whether the progress is in indeterminate mode (true = no known completion percentage).
    /// </summary>
    public bool IsIndeterminate => _isIndeterminate;

    /// <summary>
    ///     Gets the current overall progress (0-100).
    /// </summary>
    public double OverallProgress { get; private set; }

    /// <summary>
    ///     Gets the current status message (may be null if not set).
    /// </summary>
    public string? CurrentMessage => _currentMessage;

    /// <summary>
    ///     Provides a thread-safe, read-only view of the step states.
    ///     Keyed by step ID.
    /// </summary>
    public IReadOnlyDictionary<string, StepProgressState> StepStates => _stepStates;

    /// <summary>
    ///     Gets the UTC date/time when this progress state was created.
    /// </summary>
    public DateTime StartTime { get; } = DateTime.UtcNow;

    /// <summary>
    ///     Disposes of this progress state asynchronously.
    /// </summary>
    /// <remarks>
    ///     Cleans up associated <see cref="StepProgressState" /> locks
    ///     and clears internal collections.
    /// </remarks>
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
            // Dispose each step's own lock
            foreach (var state in _stepStates.Values)
            {
                await state.UpdateLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    state.UpdateLock.Dispose();
                }
                catch
                {
                    // Ignore lock disposal errors
                }
            }

            _stepStates.Clear();
            _stateLock.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }
    }

    /// <summary>
    ///     Puts the progress bar into indeterminate mode, optionally setting a <paramref name="message" />.
    /// </summary>
    /// <param name="message">A status message to display.</param>
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
    ///     Updates the overall progress (0-100) and sets a status <paramref name="message" />.
    ///     Automatically sets <see cref="IsIndeterminate" /> to false.
    /// </summary>
    /// <param name="progress">A value from 0 to 100 indicating current completion.</param>
    /// <param name="message">A status message to display.</param>
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
    ///     Retrieves or creates a <see cref="StepProgressState" /> for the specified <paramref name="stepId" />.
    /// </summary>
    /// <param name="stepId">A unique identifier for the step.</param>
    /// <returns>The existing or newly created <see cref="StepProgressState" />.</returns>
    public StepProgressState GetOrCreateStepState(string stepId)
    {
        // ConcurrentDictionary handles thread-safe get-or-add semantics
        return _stepStates.GetOrAdd(stepId, _ => new StepProgressState());
    }
}
