#region

using System.Collections.Concurrent;
using DropBear.Codex.Core;
using DropBear.Codex.StateManagement.StateSnapshots.Interfaces;
using DropBear.Codex.StateManagement.StateSnapshots.Models;
using Serilog;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots;

public sealed class SimpleSnapshotManager<T> : ISimpleSnapshotManager<T> where T : ICloneable<T>
{
    private readonly ILogger _logger = Log.Logger.ForContext<SimpleSnapshotManager<T>>();
    private readonly ConcurrentDictionary<int, Snapshot<T>> _snapshots = new();
    private T? _currentState;
    private int _currentVersion;

    public Result SaveState(T state)
    {
        try
        {
            var snapshot = new Snapshot<T>(state.Clone());
            _snapshots[Interlocked.Increment(ref _currentVersion)] = snapshot;
            _currentState = state;
            _logger.Information("Snapshot created successfully.");
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
        _logger.Information("State restored to version {Version}.", version);
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
}
