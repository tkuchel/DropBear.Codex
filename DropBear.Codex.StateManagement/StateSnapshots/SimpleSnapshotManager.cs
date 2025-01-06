#region

using System.Collections.Concurrent;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.StateManagement.StateSnapshots.Interfaces;
using DropBear.Codex.StateManagement.StateSnapshots.Models;
using Serilog;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots;

/// <summary>
///     A simple implementation of <see cref="ISimpleSnapshotManager{T}" />
///     that holds snapshots in memory, optionally taking automatic snapshots at set intervals,
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

    // Holds snapshots keyed by version number
    private readonly ConcurrentDictionary<int, Snapshot<T>> _snapshots = new();

    // Timer for automatic snapshotting (if enabled)
    private readonly Timer? _snapshotTimer;

    private T? _currentState;
    private int _currentVersion;
    private bool _disposed;
    private DateTime _lastSnapshotTime = DateTime.MinValue;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SimpleSnapshotManager{T}" /> class.
    /// </summary>
    /// <param name="snapshotInterval">How often to take snapshots if automatic snapshotting is enabled.</param>
    /// <param name="retentionTime">How long snapshots remain stored before they expire.</param>
    /// <param name="automaticSnapshotting">Whether to enable automatic snapshots at <paramref name="snapshotInterval" />.</param>
    public SimpleSnapshotManager(TimeSpan snapshotInterval, TimeSpan retentionTime, bool automaticSnapshotting)
    {
        _snapshotInterval = snapshotInterval;
        _retentionTime = retentionTime;
        _automaticSnapshotting = automaticSnapshotting;

        // If user requested automatic snapshots, schedule a timer
        if (_automaticSnapshotting)
        {
            _snapshotTimer = new Timer(TakeAutomaticSnapshot, null, _snapshotInterval, _snapshotInterval);
            _logger.Debug("Automatic snapshotting enabled with interval: {SnapshotInterval}", snapshotInterval);
        }

        _logger.Debug(
            "SimpleSnapshotManager initialized with interval: {SnapshotInterval}, retention: {RetentionTime}, automatic: {AutomaticSnapshotting}",
            snapshotInterval, retentionTime, automaticSnapshotting);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Clean up the timer
        _snapshotTimer?.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public Result SaveState(T state)
    {
        try
        {
            // If automatic snapshotting is on and we haven't reached the interval yet, skip
            if (_automaticSnapshotting && DateTime.Now - _lastSnapshotTime < _snapshotInterval)
            {
                _logger.Debug("Automatic snapshotting skipped due to snapshot interval not reached.");
                return Result.Failure("Snapshotting skipped due to interval.");
            }

            // Create a snapshot by cloning the state
            var snapshot = new Snapshot<T>(state.Clone());

            // Increment a version number and store the snapshot
            var version = Interlocked.Increment(ref _currentVersion);
            _snapshots[version] = snapshot;

            // Update internal tracking of the 'current' state and time
            _currentState = state;
            _lastSnapshotTime = DateTime.Now;

            // Remove old snapshots beyond retention
            RemoveExpiredSnapshots();

            _logger.Debug("Snapshot version {Version} created successfully.", version);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create snapshot.");
            return Result.Failure(ex.Message);
        }
    }

    /// <inheritdoc />
    public Result RestoreState(int version)
    {
        if (!_snapshots.TryGetValue(version, out var snapshot))
        {
            return Result.Failure("Snapshot not found.");
        }

        // Restore current state from the snapshot
        _currentState = snapshot.State.Clone();
        _currentVersion = version;
        _logger.Debug("State restored to version {Version}.", version);
        return Result.Success();
    }

    /// <inheritdoc />
    public Result<bool> IsDirty(T currentState)
    {
        if (_currentState == null)
        {
            // No known current state => everything is 'dirty'
            return Result<bool>.Success(true);
        }

        // Compare current stored state with the provided one
        var isDirty = !EqualityComparer<T>.Default.Equals(_currentState, currentState);
        return Result<bool>.Success(isDirty);
    }

    /// <inheritdoc />
    public Result<T?> GetCurrentState()
    {
        if (_currentState == null)
        {
            return Result<T?>.Failure("No current state.");
        }

        // Return a cloned version of the current state to avoid mutation
        return Result<T?>.Success(_currentState.Clone());
    }

    /// <summary>
    ///     Removes snapshots older than the configured retention time.
    /// </summary>
    private void RemoveExpiredSnapshots()
    {
        var expirationTime = DateTimeOffset.UtcNow - _retentionTime;
        foreach (var key in _snapshots.Keys.ToList())
        {
            var snapshot = _snapshots[key];
            if (snapshot.Timestamp < expirationTime)
            {
                // If the snapshot is expired, remove it
                _snapshots.TryRemove(key, out _);
                _logger.Debug("Snapshot with version {Version} expired and was removed.", key);
            }
        }
    }

    /// <summary>
    ///     Callback for the automatic snapshot timer. Saves the current state if available.
    /// </summary>
    /// <param name="state">Not used.</param>
    private void TakeAutomaticSnapshot(object? state)
    {
        if (_currentState != null)
        {
            // Save the existing current state as a new snapshot
            SaveState(_currentState);
            _logger.Debug("Automatic snapshot taken.");
        }
        else
        {
            _logger.Warning("No current state available for automatic snapshot.");
        }
    }
}
