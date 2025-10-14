#region

using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

#endregion

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
///     Workflow node that executes a sequence of nodes in order.
/// </summary>
public sealed class SequenceNode<TContext> : WorkflowNodeBase<TContext>
    where TContext : class
{
    private readonly List<IWorkflowNode<TContext>> _nodes;

    public SequenceNode(IEnumerable<IWorkflowNode<TContext>> nodes, string? nodeId = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        _nodes = new List<IWorkflowNode<TContext>>(nodes);
        NodeId = nodeId ?? CreateNodeId();

        if (_nodes.Count == 0)
        {
            throw new ArgumentException("Sequence node must have at least one child node", nameof(nodes));
        }
    }

    public override string NodeId { get; }

    public int NodeCount => _nodes.Count;

    public IReadOnlyList<IWorkflowNode<TContext>> Nodes => _nodes.AsReadOnly();

    public void AddNode(IWorkflowNode<TContext> node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes.Add(node);
    }

    public override async ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var executedNodes = new List<IWorkflowNode<TContext>>();
        DateTimeOffset startTime = DateTimeOffset.UtcNow;

        try
        {
            foreach (IWorkflowNode<TContext> node in _nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                NodeExecutionResult<TContext> result =
                    await node.ExecuteAsync(context, serviceProvider, cancellationToken).ConfigureAwait(false);
                executedNodes.Add(node);

                if (!result.StepResult.IsSuccess)
                {
                    if (WorkflowConstants.Signals.IsSuspensionSignal(result.StepResult.Error?.Message))
                    {
                        return result;
                    }

                    return NodeExecutionResult<TContext>.Failure(result.StepResult);
                }

                if (result.NextNodes.Count > 0)
                {
                    return NodeExecutionResult<TContext>.Success(result.NextNodes, result.StepResult.Metadata);
                }
            }

            DateTimeOffset endTime = DateTimeOffset.UtcNow;

            var metadata = new Dictionary<string, object>
(StringComparer.OrdinalIgnoreCase)
            {
                ["SequenceCompleted"] = true,
                ["NodesExecuted"] = executedNodes.Count,
                ["TotalNodes"] = _nodes.Count,
                ["ExecutionTime"] = (endTime - startTime).TotalMilliseconds
            };

            return NodeExecutionResult<TContext>.Success([], metadata);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var stepResult =
                StepResult.Failure($"Sequence execution failed at node {executedNodes.Count + 1}: {ex.Message}");
            return NodeExecutionResult<TContext>.Failure(stepResult);
        }
    }
}
