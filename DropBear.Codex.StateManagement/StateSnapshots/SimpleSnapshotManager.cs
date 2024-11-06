#region

using System.Collections.Concurrent;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.StateManagement.StateSnapshots.Interfaces;
using DropBear.Codex.StateManagement.StateSnapshots.Models;
using Serilog;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots;

public sealed class SimpleSnapshotManager<T> : ISimpleSnapshotManager<T>, IDisposable where T : ICloneable<T>
{
    private readonly bool _automaticSnapshotting;
    private readonly ILogger _logger = Log.Logger.ForContext<SimpleSnapshotManager<T>>();
    private readonly TimeSpan _retentionTime;
    private readonly TimeSpan _snapshotInterval;
    private readonly ConcurrentDictionary<int, Snapshot<T>> _snapshots = new();
    private readonly Timer? _snapshotTimer;
    private T? _currentState;
    private int _currentVersion;
    private bool _disposed;
    private DateTime _lastSnapshotTime = DateTime.MinValue;

    public SimpleSnapshotManager(TimeSpan snapshotInterval, TimeSpan retentionTime, bool automaticSnapshotting)
    {
        _snapshotInterval = snapshotInterval;
        _retentionTime = retentionTime;
        _automaticSnapshotting = automaticSnapshotting;

        // Initialize a timer for automatic snapshotting if enabled
        if (_automaticSnapshotting)
        {
            _snapshotTimer = new Timer(TakeAutomaticSnapshot, null, _snapshotInterval, _snapshotInterval);
            _logger.Debug("Automatic snapshotting enabled with interval: {SnapshotInterval}", snapshotInterval);
        }

        _logger.Debug(
            "SimpleSnapshotManager initialized with interval: {SnapshotInterval}, retention: {RetentionTime}, automatic: {AutomaticSnapshotting}",
            snapshotInterval, retentionTime, automaticSnapshotting);
    }

    // Dispose method to clean up timer
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _snapshotTimer?.Dispose();
        _disposed = true;
    }

    public Result SaveState(T state)
    {
        try
        {
            if (_automaticSnapshotting && DateTime.Now - _lastSnapshotTime < _snapshotInterval)
            {
                _logger.Debug("Automatic snapshotting skipped due to snapshot interval.");
                return Result.Failure("Snapshotting skipped due to interval.");
            }

            var snapshot = new Snapshot<T>(state.Clone());
            _snapshots[Interlocked.Increment(ref _currentVersion)] = snapshot;
            _currentState = state;
            _lastSnapshotTime = DateTime.Now;

            RemoveExpiredSnapshots();

            _logger.Debug("Snapshot created successfully.");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create snapshot.");
            return Result.Failure(ex.Message);
        }
    }

    public Result RestoreState(int version)
    {
        if (!_snapshots.TryGetValue(version, out var snapshot))
        {
            return Result.Failure("Snapshot not found.");
        }

        _currentState = snapshot.State.Clone();
        _currentVersion = version;
        _logger.Debug("State restored to version {Version}.", version);
        return Result.Success();
    }

    public Result<bool> IsDirty(T currentState)
    {
        if (_currentState == null)
        {
            return Result<bool>.Success(true);
        }

        var isDirty = !EqualityComparer<T>.Default.Equals(_currentState, currentState);
        return Result<bool>.Success(isDirty);
    }

    public Result<T?> GetCurrentState()
    {
        return _currentState == null
            ? Result<T?>.Failure("No current state.")
            : Result<T?>.Success(_currentState.Clone());
    }

    private void RemoveExpiredSnapshots()
    {
        var expirationTime = DateTimeOffset.UtcNow - _retentionTime;
        foreach (var key in _snapshots.Keys.Where(key => _snapshots[key].Timestamp < expirationTime).ToList())
        {
            _snapshots.TryRemove(key, out _);
            _logger.Debug("Snapshot with version {Version} expired and was removed.", key);
        }
    }


    // Method to handle automatic snapshotting
    private void TakeAutomaticSnapshot(object? state)
    {
        if (_currentState != null)
        {
            SaveState(_currentState);
            _logger.Debug("Automatic snapshot taken.");
        }
        else
        {
            _logger.Warning("No current state available for automatic snapshot.");
        }
    }
}
