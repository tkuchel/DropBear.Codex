#region

using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

#endregion

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
///     Workflow node that executes multiple child nodes in parallel.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public sealed class ParallelNode<TContext> : WorkflowNodeBase<TContext> where TContext : class
{
    private readonly IWorkflowNode<TContext>? _nextNode;
    private readonly IReadOnlyList<IWorkflowNode<TContext>> _parallelNodes;

    /// <summary>
    ///     Initializes a new parallel execution node.
    /// </summary>
    /// <param name="parallelNodes">Nodes to execute in parallel</param>
    /// <param name="nextNode">Node to execute after all parallel nodes complete</param>
    /// <param name="nodeId">Optional custom node ID</param>
    public ParallelNode(
        IReadOnlyList<IWorkflowNode<TContext>> parallelNodes,
        IWorkflowNode<TContext>? nextNode = null,
        string? nodeId = null)
    {
        _parallelNodes = parallelNodes ?? throw new ArgumentNullException(nameof(parallelNodes));
        _nextNode = nextNode;
        NodeId = nodeId ?? CreateNodeId();
    }

    /// <inheritdoc />
    public override string NodeId { get; }

    /// <inheritdoc />
    public override async ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        if (_parallelNodes.Count == 0)
        {
            // No parallel nodes to execute, proceed to next
            var nextNodes = _nextNode is not null ? new[] { _nextNode } : Array.Empty<IWorkflowNode<TContext>>();
            return NodeExecutionResult<TContext>.Success(nextNodes);
        }

        // Execute all parallel nodes concurrently
        var tasks = _parallelNodes.Select(node => ExecuteNodeAsync(node, context, serviceProvider, cancellationToken));
        var results = await Task.WhenAll(tasks);

        // FIXED: Check if any parallel execution failed - properly handle struct default values
        var failedResults = results.Where(r => !r.StepResult.IsSuccess).ToList();
        if (failedResults.Any())
        {
            // Return the first failure
            var firstFailure = failedResults.First();
            return NodeExecutionResult<TContext>.Failure(firstFailure.StepResult);
        }

        // All parallel executions succeeded, collect next nodes
        var allNextNodes = results
            .SelectMany(r => r.NextNodes)
            .ToList();

        // Add our own next node if specified
        if (_nextNode is not null)
        {
            allNextNodes.Add(_nextNode);
        }

        return NodeExecutionResult<TContext>.Success(allNextNodes);
    }

    /// <summary>
    ///     Executes a single node asynchronously.
    /// </summary>
    private static async Task<NodeExecutionResult<TContext>> ExecuteNodeAsync(
        IWorkflowNode<TContext> node,
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            return await node.ExecuteAsync(context, serviceProvider, cancellationToken);
        }
        catch (Exception ex)
        {
            return NodeExecutionResult<TContext>.Failure(StepResult.Failure(ex));
        }
    }
}
