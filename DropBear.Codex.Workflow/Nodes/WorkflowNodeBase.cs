using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
/// Base implementation for workflow nodes with common functionality.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public abstract class WorkflowNodeBase<TContext> : IWorkflowNode<TContext> where TContext : class
{
    /// <inheritdoc />
    public abstract string NodeId { get; }

    /// <inheritdoc />
    public abstract ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context, 
        IServiceProvider serviceProvider, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a unique node ID based on the node type and optional suffix.
    /// </summary>
    /// <param name="suffix">Optional suffix for uniqueness</param>
    /// <returns>A unique node identifier</returns>
    protected virtual string CreateNodeId(string? suffix = null)
    {
        var typeName = GetType().Name;
        return suffix is null ? typeName : $"{typeName}_{suffix}";
    }
}
