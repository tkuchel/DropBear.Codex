#region

using DropBear.Codex.StateManagement.StateSnapshots.Interfaces;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots.Builder;

public class SnapshotBuilder<T> where T : ICloneable<T>
{
    private bool _automaticSnapshotting;
    private TimeSpan _retentionTime = TimeSpan.FromHours(24);
    private TimeSpan _snapshotInterval = TimeSpan.FromMinutes(1);

    public SnapshotBuilder<T> WithAutomaticSnapshotting(bool enabled)
    {
        _automaticSnapshotting = enabled;
        return this;
    }

    public SnapshotBuilder<T> WithSnapshotInterval(TimeSpan interval)
    {
        if (interval < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentException("Snapshot interval must be at least one second.", nameof(interval));
        }

        _snapshotInterval = interval;
        return this;
    }

    public SnapshotBuilder<T> WithRetentionTime(TimeSpan retentionTime)
    {
        if (retentionTime < TimeSpan.Zero)
        {
            throw new ArgumentException("Retention time cannot be negative.", nameof(retentionTime));
        }

        _retentionTime = retentionTime;
        return this;
    }

    public ISimpleSnapshotManager<T> Build()
    {
        return new SimpleSnapshotManager<T>();
    }
}
