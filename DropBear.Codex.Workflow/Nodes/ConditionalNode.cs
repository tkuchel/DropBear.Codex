using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
/// Workflow node that provides conditional branching based on context state.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public sealed class ConditionalNode<TContext> : WorkflowNodeBase<TContext> where TContext : class
{
    private readonly Func<TContext, bool> _condition;
    private readonly IWorkflowNode<TContext>? _trueNode;
    private readonly IWorkflowNode<TContext>? _falseNode;
    private readonly string _nodeId;

    /// <summary>
    /// Initializes a new conditional branching node.
    /// </summary>
    /// <param name="condition">Predicate to evaluate the condition</param>
    /// <param name="trueNode">Node to execute when condition is true</param>
    /// <param name="falseNode">Node to execute when condition is false</param>
    /// <param name="nodeId">Optional custom node ID</param>
    public ConditionalNode(
        Func<TContext, bool> condition,
        IWorkflowNode<TContext>? trueNode = null,
        IWorkflowNode<TContext>? falseNode = null,
        string? nodeId = null)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _trueNode = trueNode;
        _falseNode = falseNode;
        _nodeId = nodeId ?? CreateNodeId();
    }

    /// <inheritdoc />
    public override string NodeId => _nodeId;

    /// <inheritdoc />
    public override ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Evaluate the condition
            var conditionResult = _condition(context);
            
            // Select the appropriate next node
            var nextNode = conditionResult ? _trueNode : _falseNode;
            var nextNodes = nextNode is not null ? new[] { nextNode } : Array.Empty<IWorkflowNode<TContext>>();

            // Create metadata about the branching decision
            var metadata = new Dictionary<string, object>
            {
                ["ConditionResult"] = conditionResult,
                ["BranchTaken"] = conditionResult ? "True" : "False"
            };

            return ValueTask.FromResult(NodeExecutionResult<TContext>.Success(nextNodes, metadata));
        }
        catch (Exception ex)
        {
            // Condition evaluation failed
            var stepResult = StepResult.Failure($"Condition evaluation failed: {ex.Message}", false);
            return ValueTask.FromResult(NodeExecutionResult<TContext>.Failure(stepResult));
        }
    }
}
