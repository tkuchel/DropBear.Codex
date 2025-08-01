using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
/// Workflow node that introduces a delay in execution.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public sealed class DelayNode<TContext> : WorkflowNodeBase<TContext> where TContext : class
{
    private readonly TimeSpan _delay;
    private readonly IWorkflowNode<TContext>? _nextNode;
    private readonly string _nodeId;

    /// <summary>
    /// Initializes a new delay node.
    /// </summary>
    /// <param name="delay">Duration to delay execution</param>
    /// <param name="nextNode">Node to execute after the delay</param>
    /// <param name="nodeId">Optional custom node ID</param>
    public DelayNode(TimeSpan delay, IWorkflowNode<TContext>? nextNode = null, string? nodeId = null)
    {
        _delay = delay;
        _nextNode = nextNode;
        _nodeId = nodeId ?? CreateNodeId();
    }

    /// <inheritdoc />
    public override string NodeId => _nodeId;

    /// <inheritdoc />
    public override async ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Wait for the specified delay
            await Task.Delay(_delay, cancellationToken);

            // Proceed to next node
            var nextNodes = _nextNode is not null ? new[] { _nextNode } : Array.Empty<IWorkflowNode<TContext>>();
            var metadata = new Dictionary<string, object> { ["DelayDuration"] = _delay };

            return NodeExecutionResult<TContext>.Success(nextNodes, metadata);
        }
        catch (OperationCanceledException)
        {
            // Cancellation during delay is expected
            throw;
        }
        catch (Exception ex)
        {
            // Unexpected error during delay
            var stepResult = StepResult.Failure($"Delay node failed: {ex.Message}", false);
            return NodeExecutionResult<TContext>.Failure(stepResult);
        }
    }
}
