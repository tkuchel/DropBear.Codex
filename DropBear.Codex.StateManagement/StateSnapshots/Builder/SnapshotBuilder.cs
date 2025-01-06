#region

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
    /// <param name="enabled"><c>true</c> to enable, <c>false</c> to disable.</param>
    /// <returns>This <see cref="SnapshotBuilder{T}" /> instance for fluent chaining.</returns>
    public SnapshotBuilder<T> WithAutomaticSnapshotting(bool enabled)
    {
        _automaticSnapshotting = enabled;
        return this;
    }

    /// <summary>
    ///     Sets the interval at which snapshots will be created automatically (if enabled).
    /// </summary>
    /// <param name="interval">A <see cref="TimeSpan" /> representing how frequently snapshots are taken.</param>
    /// <returns>This <see cref="SnapshotBuilder{T}" /> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="interval" /> is less than one second.</exception>
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
    ///     Sets how long snapshots are retained before being discarded.
    /// </summary>
    /// <param name="retentionTime">A <see cref="TimeSpan" /> for snapshot retention.</param>
    /// <returns>This <see cref="SnapshotBuilder{T}" /> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="retentionTime" /> is negative.</exception>
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
    ///     Builds and returns an <see cref="ISimpleSnapshotManager{T}" /> based on the configuration set in this builder.
    /// </summary>
    /// <returns>
    ///     An <see cref="ISimpleSnapshotManager{T}" /> instance configured with snapshot interval, retention time, and
    ///     automatic snapshotting.
    /// </returns>
    public ISimpleSnapshotManager<T> Build()
    {
        return new SimpleSnapshotManager<T>(_snapshotInterval, _retentionTime, _automaticSnapshotting);
    }
}
