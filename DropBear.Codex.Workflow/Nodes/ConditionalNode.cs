#region

using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

#endregion

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
///     Workflow node that provides conditional branching based on context state.
/// </summary>
public sealed class ConditionalNode<TContext> : WorkflowNodeBase<TContext>
    where TContext : class
{
    private readonly Func<TContext, bool> _condition;

    public ConditionalNode(
        Func<TContext, bool> condition,
        IWorkflowNode<TContext>? trueNode = null,
        IWorkflowNode<TContext>? falseNode = null,
        string? nodeId = null)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        TrueNode = trueNode;
        FalseNode = falseNode;
        NodeId = nodeId ?? CreateNodeId();
    }

    public override string NodeId { get; }

    public IWorkflowNode<TContext>? TrueNode { get; }

    public IWorkflowNode<TContext>? FalseNode { get; }

    public override ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            bool conditionResult = _condition(context);

            IWorkflowNode<TContext>? nextNode = conditionResult ? TrueNode : FalseNode;
            IWorkflowNode<TContext>[] nextNodes = nextNode is not null
                ? new[] { nextNode }
                : Array.Empty<IWorkflowNode<TContext>>();

            var metadata = new Dictionary<string, object>
            {
                ["ConditionResult"] = conditionResult, ["BranchTaken"] = conditionResult ? "True" : "False"
            };

            return ValueTask.FromResult(
                NodeExecutionResult<TContext>.Success(nextNodes, metadata));
        }
        catch (Exception ex)
        {
            var stepResult = StepResult.Failure($"Conditional evaluation failed: {ex.Message}");
            return ValueTask.FromResult(NodeExecutionResult<TContext>.Failure(stepResult));
        }
    }
}
