#region

using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

#endregion

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
///     Workflow node that introduces a delay in execution.
/// </summary>
public sealed class DelayNode<TContext> : WorkflowNodeBase<TContext>, ILinkableNode<TContext>
    where TContext : class
{
    private readonly TimeSpan _delay;
    private IWorkflowNode<TContext>? _nextNode;

    public DelayNode(TimeSpan delay, IWorkflowNode<TContext>? nextNode = null, string? nodeId = null)
    {
        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay must be greater than zero.");
        }

        _delay = delay;
        _nextNode = nextNode;
        NodeId = nodeId ?? CreateNodeId();
    }

    public override string NodeId { get; }

    public TimeSpan Delay => _delay;

    public void SetNextNode(IWorkflowNode<TContext>? nextNode) => _nextNode = nextNode;

    public IWorkflowNode<TContext>? GetNextNode() => _nextNode;

    public override async ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);

            IWorkflowNode<TContext>[] nextNodes = _nextNode is not null
                ? new[] { _nextNode }
                : Array.Empty<IWorkflowNode<TContext>>();

            var metadata = new Dictionary<string, object> { ["DelayDuration"] = _delay.TotalMilliseconds };

            return NodeExecutionResult<TContext>.Success(nextNodes, metadata);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var stepResult = StepResult.Failure($"Delay node failed: {ex.Message}");
            return NodeExecutionResult<TContext>.Failure(stepResult);
        }
    }
}
