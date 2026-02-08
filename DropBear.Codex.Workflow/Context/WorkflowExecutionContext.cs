using DropBear.Codex.Workflow.Configuration;

namespace DropBear.Codex.Workflow.Context;

/// <summary>
///     Default implementation of workflow execution context.
///     Note: This class will be instantiated via dependency injection when properly configured.
/// </summary>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes - will be used via DI
internal sealed class WorkflowExecutionContext : IWorkflowExecutionContext
#pragma warning restore CA1812
{
    public WorkflowExecutionContext(
        WorkflowExecutionOptions options,
        string correlationId,
        string workflowId)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        WorkflowId = workflowId ?? throw new ArgumentNullException(nameof(workflowId));
    }

    public WorkflowExecutionOptions Options { get; }
    public string CorrelationId { get; }
    public string WorkflowId { get; }
}
