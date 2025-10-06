namespace DropBear.Codex.Workflow.Interfaces;

/// <summary>
/// Represents a node in the workflow execution graph.
/// Can be a single step, conditional branch, parallel execution, etc.
/// </summary>
/// <typeparam name="TContext">The type of context that flows through the workflow</typeparam>
public interface IWorkflowNode<TContext> where TContext : class
{
    /// <summary>
    /// Gets the unique identifier for this node.
    /// </summary>
    string NodeId { get; }

    /// <summary>
    /// Executes this node and returns the next nodes to execute.
    /// </summary>
    /// <param name="context">The workflow context</param>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Execution result and next nodes to process</returns>
    ValueTask<Results.NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context, 
        IServiceProvider serviceProvider, 
        CancellationToken cancellationToken = default);
}
