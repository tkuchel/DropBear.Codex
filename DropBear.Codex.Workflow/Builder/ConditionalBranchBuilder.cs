#region

using DropBear.Codex.Workflow.Exceptions;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Nodes;

#endregion

namespace DropBear.Codex.Workflow.Builder;

/// <summary>
///     Builder for conditional branches in workflows.
/// </summary>
public sealed class ConditionalBranchBuilder<TContext> where TContext : class
{
    private readonly IWorkflowNode<TContext> _branchPoint;
    private readonly Func<TContext, bool> _condition;
    private readonly string? _nodeId;
    private readonly WorkflowBuilder<TContext> _parentBuilder;
    private IWorkflowNode<TContext>? _falseNode;
    private IWorkflowNode<TContext>? _trueNode;

    internal ConditionalBranchBuilder(
        WorkflowBuilder<TContext> parentBuilder,
        IWorkflowNode<TContext> branchPoint,
        Func<TContext, bool> condition,
        string? nodeId)
    {
        _parentBuilder = parentBuilder ?? throw new ArgumentNullException(nameof(parentBuilder));
        _branchPoint = branchPoint ?? throw new ArgumentNullException(nameof(branchPoint));
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _nodeId = nodeId;
    }

    private string? WorkflowId => _parentBuilder.WorkflowId;

    public ConditionalBranchBuilder<TContext> ThenExecute<TStep>(string? nodeId = null)
        where TStep : class, IWorkflowStep<TContext>
    {
        if (_trueNode is not null)
        {
            throw new WorkflowConfigurationException(
                "True branch already configured. Call EndIf() before adding another condition.",
                _parentBuilder.WorkflowId);
        }

        _trueNode = new StepNode<TContext, TStep>(null, nodeId);
        return this;
    }

    public ConditionalBranchBuilder<TContext> ElseExecute<TStep>(string? nodeId = null)
        where TStep : class, IWorkflowStep<TContext>
    {
        if (_falseNode is not null)
        {
            throw new WorkflowConfigurationException(
                "False branch already configured. Call EndIf() before adding another condition.",
                _parentBuilder.WorkflowId);
        }

        _falseNode = new StepNode<TContext, TStep>(null, nodeId);
        return this;
    }

    public ConditionalBranchBuilder<TContext> Then(Action<WorkflowBuilder<TContext>> configureBranch)
    {
        ArgumentNullException.ThrowIfNull(configureBranch);

        if (_trueNode is not null)
        {
            throw new WorkflowConfigurationException(
                "True branch already configured. Call EndIf() before adding another condition.",
                _parentBuilder.WorkflowId);
        }

        var branchBuilder = new WorkflowBuilder<TContext>(
            $"{_parentBuilder.WorkflowId ?? "conditional"}-true",
            "True Branch");

        configureBranch(branchBuilder);

        IWorkflowDefinition<TContext> definition = branchBuilder.Build();
        _trueNode = definition.BuildWorkflow();

        return this;
    }

    public ConditionalBranchBuilder<TContext> Else(Action<WorkflowBuilder<TContext>> configureBranch)
    {
        ArgumentNullException.ThrowIfNull(configureBranch);

        if (_falseNode is not null)
        {
            throw new WorkflowConfigurationException(
                "False branch already configured. Call EndIf() before adding another condition.",
                _parentBuilder.WorkflowId);
        }

        var branchBuilder = new WorkflowBuilder<TContext>(
            $"{_parentBuilder.WorkflowId ?? "conditional"}-false",
            "False Branch");

        configureBranch(branchBuilder);

        IWorkflowDefinition<TContext> definition = branchBuilder.Build();
        _falseNode = definition.BuildWorkflow();

        return this;
    }

    public WorkflowBuilder<TContext> EndIf()
    {
        var conditionalNode = new ConditionalNode<TContext>(_condition, _trueNode, _falseNode, _nodeId);
        _parentBuilder.LinkNodes(_branchPoint, conditionalNode);
        _parentBuilder.SetCurrentNode(conditionalNode);

        return _parentBuilder;
    }
}
