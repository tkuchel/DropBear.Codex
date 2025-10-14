#region

using DropBear.Codex.Workflow.Common;

#endregion

namespace DropBear.Codex.Workflow.Configuration;

/// <summary>
///     Configuration options for persistent workflows.
/// </summary>
public sealed class PersistentWorkflowOptions
{
    /// <summary>
    ///     Whether to enable automatic timeout processing for waiting workflows.
    /// </summary>
    public bool EnableTimeoutProcessing { get; set; } = true;

    /// <summary>
    ///     Interval for checking workflow timeouts.
    /// </summary>
    public TimeSpan TimeoutCheckInterval { get; set; } = WorkflowConstants.Defaults.DefaultTimeoutCheckInterval;

    /// <summary>
    ///     Default signal timeout for persistent workflows.
    /// </summary>
    public TimeSpan DefaultSignalTimeout { get; set; } = WorkflowConstants.Defaults.DefaultSignalTimeout;
}
