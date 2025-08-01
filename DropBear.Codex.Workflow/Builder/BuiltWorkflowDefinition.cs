using DropBear.Codex.Workflow.Interfaces;

namespace DropBear.Codex.Workflow.Builder;

/// <summary>
/// Internal implementation of workflow definition created by the builder.
/// </summary>
internal sealed record BuiltWorkflowDefinition<TContext>(
    string WorkflowId,
    string DisplayName,
    Version Version,
    TimeSpan? WorkflowTimeout,
    IWorkflowNode<TContext> RootNode) : IWorkflowDefinition<TContext> where TContext : class
{
    public IWorkflowNode<TContext> BuildWorkflow() => RootNode;
}
