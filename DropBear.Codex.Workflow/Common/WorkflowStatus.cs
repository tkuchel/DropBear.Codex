namespace DropBear.Codex.Workflow.Common;

/// <summary>
///     Represents the current status of a workflow instance.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>
    ///     Workflow is currently running.
    /// </summary>
    Running = 0,

    /// <summary>
    ///     Workflow is waiting for an external signal to continue.
    /// </summary>
    WaitingForSignal = 1,

    /// <summary>
    ///     Workflow is waiting for human approval.
    /// </summary>
    WaitingForApproval = 2,

    /// <summary>
    ///     Workflow completed successfully.
    /// </summary>
    Completed = 3,

    /// <summary>
    ///     Workflow failed with an error.
    /// </summary>
    Failed = 4,

    /// <summary>
    ///     Workflow was cancelled.
    /// </summary>
    Cancelled = 5,

    /// <summary>
    ///     Workflow timed out.
    /// </summary>
    TimedOut = 6
}
