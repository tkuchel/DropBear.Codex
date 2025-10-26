#region

using System.Collections.Concurrent;
using DropBear.Codex.Core.Results;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.StateManagement.Errors;
using DropBear.Codex.StateManagement.StateSnapshots.Interfaces;
using DropBear.Codex.StateManagement.StateSnapshots.Models;
using Serilog;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots;

/// <summary>
///     A simple implementation of <see cref="ISimpleSnapshotManager{T}" />
///     that holds snapshots in memory, optionally taking automatic snapshots at set intervals
///     and discarding snapshots older than the configured retention time.
/// </summary>
/// <typeparam name="T">A type that implements <see cref="ICloneable{T}" /> for snapshotting.</typeparam>
public sealed class SimpleSnapshotManager<T> : ISimpleSnapshotManager<T>, IDisposable
    where T : ICloneable<T>
{
    private readonly bool _automaticSnapshotting;
    private readonly ILogger _logger = Log.Logger.ForContext<SimpleSnapshotManager<T>>();
    private readonly TimeSpan _retentionTime;
    private readonly TimeSpan _snapshotInterval;

    // Holds snapshots keyed by version
    private readonly ConcurrentDictionary<int, Snapshot<T>> _snapshots = new();

    private readonly Timer? _snapshotTimer;
    private T? _currentState;
    private int _currentVersion;
    private bool _disposed;
    private DateTime _lastSnapshotTime = DateTime.MinValue;

    /// <summary>
    ///     Initializes a new instance of <see cref="SimpleSnapshotManager{T}" />.
    /// </summary>
    public SimpleSnapshotManager(TimeSpan snapshotInterval, TimeSpan retentionTime, bool automaticSnapshotting)
    {
        _snapshotInterval = snapshotInterval;
        _retentionTime = retentionTime;
        _automaticSnapshotting = automaticSnapshotting;

        if (_automaticSnapshotting)
        {
            _snapshotTimer = new Timer(TakeAutomaticSnapshot, null, _snapshotInterval, _snapshotInterval);
            _logger.Debug("Automatic snapshotting enabled with interval: {SnapshotInterval}", snapshotInterval);
        }

        _logger.Debug(
            "SimpleSnapshotManager initialized: interval={SnapshotInterval}, retention={RetentionTime}, automatic={AutomaticSnapshotting}",
            snapshotInterval, retentionTime, automaticSnapshotting);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _snapshotTimer?.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public Result<Unit, SnapshotError> SaveState(T state)
    {
        try
        {
            // *** CHANGE *** optional concurrency control
            // lock (this) { … } or a dedicated lock if multiple threads calling SaveState.
            if (_automaticSnapshotting && DateTime.Now - _lastSnapshotTime < _snapshotInterval)
            {
                _logger.Debug("Automatic snapshotting skipped due to snapshot interval not reached.");
                return Result<Unit, SnapshotError>.Failure(SnapshotError.IntervalNotReached());
            }

            var snapshot = new Snapshot<T>(state.Clone());
            var version = Interlocked.Increment(ref _currentVersion);
            _snapshots[version] = snapshot;

            _currentState = state;
            _lastSnapshotTime = DateTime.Now;

            RemoveExpiredSnapshots();
            _logger.Debug("Snapshot version {Version} created successfully.", version);
            return Result<Unit, SnapshotError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create snapshot.");
            return Result<Unit, SnapshotError>.Failure(
                SnapshotError.CreationFailed(ex.Message).WithContext(nameof(SaveState)),
                ex);
        }
    }

    /// <inheritdoc />
    public Result<Unit, SnapshotError> RestoreState(int version)
    {
        if (!_snapshots.TryGetValue(version, out var snapshot))
        {
            return Result<Unit, SnapshotError>.Failure(SnapshotError.NotFound(version));
        }

        _currentState = snapshot.State.Clone();
        _currentVersion = version;
        _logger.Debug("State restored to version {Version}.", version);
        return Result<Unit, SnapshotError>.Success(Unit.Value);
    }

    /// <inheritdoc />
    public Result<bool, SnapshotError> IsDirty(T currentState)
    {
        if (_currentState == null)
        {
            // No known current => everything is dirty
            return Result<bool, SnapshotError>.Success(true);
        }

        var isDirty = !EqualityComparer<T>.Default.Equals(_currentState, currentState);
        return Result<bool, SnapshotError>.Success(isDirty);
    }

    /// <inheritdoc />
    public Result<T?, SnapshotError> GetCurrentState()
    {
        if (_currentState == null)
        {
            return Result<T?, SnapshotError>.Failure(SnapshotError.NoCurrentState());
        }

        // Return cloned version
        return Result<T?, SnapshotError>.Success(_currentState.Clone());
    }

    /// <summary>
    ///     Removes snapshots older than <see cref="_retentionTime" />.
    /// </summary>
    private void RemoveExpiredSnapshots()
    {
        var expirationTime = DateTimeOffset.UtcNow - _retentionTime;
        foreach (var kvp in _snapshots)
        {
            if (kvp.Value.Timestamp < expirationTime)
            {
                // If the snapshot is expired, remove it
                if (_snapshots.TryRemove(kvp.Key, out _))
                {
                    _logger.Debug("Snapshot with version {Version} expired and was removed.", kvp.Key);
                }
            }
        }
    }

    /// <summary>
    ///     Callback for the automatic snapshot timer. Saves the current state if available.
    /// </summary>
    private void TakeAutomaticSnapshot(object? state)
    {
        if (_currentState != null && !_disposed)
        {
            SaveState(_currentState);
            _logger.Debug("Automatic snapshot taken.");
        }
        else
        {
            _logger.Warning("No current state available for automatic snapshot, or manager disposed.");
        }
    }
}
