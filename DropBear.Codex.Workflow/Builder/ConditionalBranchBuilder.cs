using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Nodes;

namespace DropBear.Codex.Workflow.Builder;

/// <summary>
/// Builder for conditional branches in workflows.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public sealed class ConditionalBranchBuilder<TContext> where TContext : class
{
    private readonly WorkflowBuilder<TContext> _parentBuilder;
    private readonly IWorkflowNode<TContext> _branchPoint;
    private readonly Func<TContext, bool> _condition;
    private readonly string? _nodeId;
    private IWorkflowNode<TContext>? _trueNode;
    private IWorkflowNode<TContext>? _falseNode;

    internal ConditionalBranchBuilder(
        WorkflowBuilder<TContext> parentBuilder,
        IWorkflowNode<TContext> branchPoint,
        Func<TContext, bool> condition,
        string? nodeId)
    {
        _parentBuilder = parentBuilder;
        _branchPoint = branchPoint;
        _condition = condition;
        _nodeId = nodeId;
    }

    /// <summary>
    /// Defines the step to execute when the condition is true.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <param name="nodeId">Optional custom node ID</param>
    /// <returns>The conditional branch builder for method chaining</returns>
    public ConditionalBranchBuilder<TContext> ThenExecute<TStep>(string? nodeId = null)
        where TStep : class, IWorkflowStep<TContext>
    {
        _trueNode = new StepNode<TContext, TStep>(null, nodeId);
        return this;
    }

    /// <summary>
    /// Defines the step to execute when the condition is false.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <param name="nodeId">Optional custom node ID</param>
    /// <returns>The conditional branch builder for method chaining</returns>
    public ConditionalBranchBuilder<TContext> ElseExecute<TStep>(string? nodeId = null)
        where TStep : class, IWorkflowStep<TContext>
    {
        _falseNode = new StepNode<TContext, TStep>(null, nodeId);
        return this;
    }

    /// <summary>
    /// Completes the conditional branch and returns to the main workflow builder.
    /// </summary>
    /// <returns>The parent workflow builder</returns>
    public WorkflowBuilder<TContext> EndIf()
    {
        var conditionalNode = new ConditionalNode<TContext>(_condition, _trueNode, _falseNode, _nodeId);
        _parentBuilder.LinkNodes(_branchPoint, conditionalNode);
        _parentBuilder.SetCurrentNode(conditionalNode);
        return _parentBuilder;
    }
}
