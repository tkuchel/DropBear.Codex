#region

using DropBear.Codex.Workflow.Common;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Configuration;

/// <summary>
///     Configuration options for persistent workflow execution.
/// </summary>
public sealed class PersistentWorkflowOptions
{
    /// <summary>
    ///     Gets or sets a value indicating whether timeout processing is enabled.
    /// </summary>
    /// <remarks>
    ///     When enabled, the workflow timeout service will periodically check for
    ///     workflows that have exceeded their signal timeout and cancel them.
    /// </remarks>
    public bool EnableTimeoutProcessing { get; set; } = true;

    /// <summary>
    ///     Gets or sets the interval at which the system checks for timed-out workflows.
    /// </summary>
    /// <remarks>
    ///     Default is 5 minutes. Shorter intervals provide faster timeout detection
    ///     but increase system load.
    /// </remarks>
    public TimeSpan TimeoutCheckInterval { get; set; } = WorkflowConstants.Defaults.DefaultTimeoutCheckInterval;

    /// <summary>
    ///     Gets or sets the default signal timeout for workflows.
    /// </summary>
    /// <remarks>
    ///     This is the default timeout used when a workflow suspends and waits for a signal,
    ///     unless a specific timeout is provided. Default is 24 hours.
    /// </remarks>
    public TimeSpan DefaultSignalTimeout { get; set; } = WorkflowConstants.Defaults.DefaultSignalTimeout;

    /// <summary>
    ///     Gets or sets a value indicating whether to enable workflow state snapshots.
    /// </summary>
    /// <remarks>
    ///     When enabled, the system will periodically snapshot workflow state for recovery purposes.
    /// </remarks>
    public bool EnableStateSnapshots { get; set; }

    /// <summary>
    ///     Gets or sets the interval at which workflow state snapshots are created.
    /// </summary>
    /// <remarks>
    ///     Only used when EnableStateSnapshots is true. Default is 1 hour.
    /// </remarks>
    public TimeSpan SnapshotInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    ///     Gets or sets the maximum number of workflow instances to process in a single timeout check.
    /// </summary>
    /// <remarks>
    ///     This prevents the timeout service from being overwhelmed by a large number of timed-out workflows.
    ///     Default is 100.
    /// </remarks>
    public int MaxTimeoutBatchSize { get; set; } = 100;

    /// <summary>
    ///     Gets or sets a value indicating whether to enable detailed logging for persistent workflows.
    /// </summary>
    public bool EnableDetailedLogging { get; set; }

    /// <summary>
    ///     Validates the options and throws an exception if any values are invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option value is out of valid range</exception>
    public void Validate()
    {
        if (TimeoutCheckInterval < TimeSpan.FromSeconds(10))
        {
            throw new ArgumentOutOfRangeException(
                nameof(TimeoutCheckInterval),
                TimeoutCheckInterval,
                "Timeout check interval must be at least 10 seconds");
        }

        if (TimeoutCheckInterval > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(
                nameof(TimeoutCheckInterval),
                TimeoutCheckInterval,
                "Timeout check interval must not exceed 24 hours");
        }

        if (DefaultSignalTimeout < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(DefaultSignalTimeout),
                DefaultSignalTimeout,
                "Default signal timeout must be at least 1 second");
        }

        if (SnapshotInterval < TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(SnapshotInterval),
                SnapshotInterval,
                "Snapshot interval must be at least 1 minute");
        }

        if (MaxTimeoutBatchSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxTimeoutBatchSize),
                MaxTimeoutBatchSize,
                "Max timeout batch size must be at least 1");
        }

        if (MaxTimeoutBatchSize > 10000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxTimeoutBatchSize),
                MaxTimeoutBatchSize,
                "Max timeout batch size must not exceed 10000");
        }
    }
}
