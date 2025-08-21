using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
/// Workflow node that executes a sequence of child nodes in order.
/// Stops execution if any node fails or signals suspension.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public sealed class SequenceNode<TContext> : WorkflowNodeBase<TContext> where TContext : class
{
    private readonly List<IWorkflowNode<TContext>> _nodes;
    private readonly string _nodeId;

    /// <summary>
    /// Initializes a new sequence node with the given child nodes.
    /// </summary>
    /// <param name="nodes">The nodes to execute in sequence</param>
    /// <param name="nodeId">Optional custom node ID</param>
    public SequenceNode(IEnumerable<IWorkflowNode<TContext>> nodes, string? nodeId = null)
    {
        _nodes = new List<IWorkflowNode<TContext>>(nodes ?? throw new ArgumentNullException(nameof(nodes)));
        _nodeId = nodeId ?? CreateNodeId();

        if (_nodes.Count == 0)
        {
            throw new ArgumentException("Sequence node must have at least one child node", nameof(nodes));
        }
    }

    /// <inheritdoc />
    public override string NodeId => _nodeId;

    /// <summary>
    /// Adds a node to the end of the sequence.
    /// </summary>
    /// <param name="node">The node to add</param>
    public void AddNode(IWorkflowNode<TContext> node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes.Add(node);
    }

    /// <inheritdoc />
    public override async ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var executedNodes = new List<IWorkflowNode<TContext>>();

        try
        {
            foreach (var node in _nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await node.ExecuteAsync(context, serviceProvider, cancellationToken);
                executedNodes.Add(node);

                // If this node failed, return the failure
                if (!result.StepResult.IsSuccess)
                {
                    // Check if this is a suspension signal - these should be propagated immediately
                    if (IsSuspensionSignal(result.StepResult))
                    {
                        return result; // Propagate suspension signal
                    }

                    // Regular failure - stop execution
                    return NodeExecutionResult<TContext>.Failure(result.StepResult);
                }

                // If this node has next nodes, we need to continue with those instead of our sequence
                if (result.NextNodes.Count > 0)
                {
                    return NodeExecutionResult<TContext>.Success(result.NextNodes, result.StepResult.Metadata);
                }

                // Continue with the next node in our sequence
            }

            // All nodes in sequence completed successfully
            var metadata = new Dictionary<string, object>
            {
                ["SequenceCompleted"] = true, ["NodesExecuted"] = executedNodes.Count
            };

            return NodeExecutionResult<TContext>.Success(Array.Empty<IWorkflowNode<TContext>>(), metadata);
        }
        catch (Exception ex)
        {
            var stepResult =
                StepResult.Failure($"Sequence execution failed at node {executedNodes.Count + 1}: {ex.Message}", false);
            return NodeExecutionResult<TContext>.Failure(stepResult);
        }
    }

    /// <summary>
    /// Checks if a step result represents a suspension signal.
    /// </summary>
    private static bool IsSuspensionSignal(StepResult stepResult)
    {
        return !stepResult.IsSuccess &&
               stepResult.ErrorMessage?.StartsWith("WAITING_FOR_SIGNAL:", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Gets the nodes in this sequence (for debugging/inspection).
    /// </summary>
    public IReadOnlyList<IWorkflowNode<TContext>> Nodes => _nodes.AsReadOnly();
}
