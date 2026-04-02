using DropBear.Codex.Workflow.Configuration;

namespace DropBear.Codex.Workflow.Context;

/// <summary>
///     Provides access to the current workflow execution context and options.
///     This is registered as a scoped service during workflow execution.
/// </summary>
public interface IWorkflowExecutionContext
{
    /// <summary>
    ///     Gets the execution options for the current workflow execution.
    /// </summary>
    WorkflowExecutionOptions Options { get; }

    /// <summary>
    ///     Gets the correlation ID for the current execution.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    ///     Gets the workflow ID being executed.
    /// </summary>
    string WorkflowId { get; }
}
