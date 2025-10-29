#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.StateManagement.Errors;
using DropBear.Codex.StateManagement.StateSnapshots.Interfaces;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots.Builder;

/// <summary>
///     A builder class for configuring and creating a simple snapshot manager of type <typeparamref name="T" />.
///     Supports automatic snapshotting, snapshot intervals, and retention time.
/// </summary>
/// <typeparam name="T">A cloneable type implementing <see cref="ICloneable{T}" />.</typeparam>
public sealed class SnapshotBuilder<T> where T : ICloneable<T>
{
    private bool _automaticSnapshotting;
    private TimeSpan _retentionTime = TimeSpan.FromHours(24);
    private TimeSpan _snapshotInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     Enables or disables automatic snapshotting.
    /// </summary>
    public SnapshotBuilder<T> WithAutomaticSnapshotting(bool enabled)
    {
        _automaticSnapshotting = enabled;
        return this;
    }

    /// <summary>
    ///     Sets the interval at which snapshots will be created automatically (if enabled).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if interval is less than one second.</exception>
    public SnapshotBuilder<T> WithSnapshotInterval(TimeSpan interval)
    {
        if (interval < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentException("Snapshot interval must be at least one second.", nameof(interval));
        }

        _snapshotInterval = interval;
        return this;
    }

    /// <summary>
    ///     Sets the interval at which snapshots will be created automatically (if enabled).
    ///     Returns a Result instead of throwing exceptions.
    /// </summary>
    public Result<SnapshotBuilder<T>, BuilderError> TryWithSnapshotInterval(TimeSpan interval)
    {
        if (interval < TimeSpan.FromSeconds(1))
        {
            return Result<SnapshotBuilder<T>, BuilderError>.Failure(
                BuilderError.InvalidInterval(nameof(interval), "Snapshot interval must be at least one second."));
        }

        _snapshotInterval = interval;
        return Result<SnapshotBuilder<T>, BuilderError>.Success(this);
    }

    /// <summary>
    ///     Sets how long snapshots are retained before being discarded.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if retention time is negative.</exception>
    public SnapshotBuilder<T> WithRetentionTime(TimeSpan retentionTime)
    {
        if (retentionTime < TimeSpan.Zero)
        {
            throw new ArgumentException("Retention time cannot be negative.", nameof(retentionTime));
        }

        _retentionTime = retentionTime;
        return this;
    }

    /// <summary>
    ///     Sets how long snapshots are retained before being discarded.
    ///     Returns a Result instead of throwing exceptions.
    /// </summary>
    public Result<SnapshotBuilder<T>, BuilderError> TryWithRetentionTime(TimeSpan retentionTime)
    {
        if (retentionTime < TimeSpan.Zero)
        {
            return Result<SnapshotBuilder<T>, BuilderError>.Failure(
                BuilderError.InvalidRetentionTime("Retention time cannot be negative."));
        }

        _retentionTime = retentionTime;
        return Result<SnapshotBuilder<T>, BuilderError>.Success(this);
    }

    /// <summary>
    ///     Builds and returns an <see cref="ISimpleSnapshotManager{T}" /> based on the configuration.
    /// </summary>
    public ISimpleSnapshotManager<T> Build()
    {
        return new SimpleSnapshotManager<T>(_snapshotInterval, _retentionTime, _automaticSnapshotting);
    }

    /// <summary>
    ///     Attempts to build and return an <see cref="ISimpleSnapshotManager{T}" /> based on the configuration.
    ///     Returns a Result instead of throwing exceptions.
    /// </summary>
    public Result<ISimpleSnapshotManager<T>, BuilderError> TryBuild()
    {
        try
        {
            var manager = new SimpleSnapshotManager<T>(_snapshotInterval, _retentionTime, _automaticSnapshotting);
            return Result<ISimpleSnapshotManager<T>, BuilderError>.Success(manager);
        }
        catch (Exception ex)
        {
            return Result<ISimpleSnapshotManager<T>, BuilderError>.Failure(
                BuilderError.FromException(ex).WithContext(nameof(TryBuild)),
                ex);
        }
    }
}
