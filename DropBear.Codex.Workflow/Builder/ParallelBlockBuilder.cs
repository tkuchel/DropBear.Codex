using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Nodes;

namespace DropBear.Codex.Workflow.Builder;

/// <summary>
/// Builder for parallel execution blocks in workflows.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public sealed class ParallelBlockBuilder<TContext> where TContext : class
{
    private readonly WorkflowBuilder<TContext> _parentBuilder;
    private readonly IWorkflowNode<TContext> _branchPoint;
    private readonly string? _nodeId;
    private readonly List<IWorkflowNode<TContext>> _parallelNodes = new();

    internal ParallelBlockBuilder(
        WorkflowBuilder<TContext> parentBuilder,
        IWorkflowNode<TContext> branchPoint,
        string? nodeId)
    {
        _parentBuilder = parentBuilder;
        _branchPoint = branchPoint;
        _nodeId = nodeId;
    }

    /// <summary>
    /// Adds a step to execute in parallel.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute in parallel</typeparam>
    /// <param name="nodeId">Optional custom node ID</param>
    /// <returns>The parallel block builder for method chaining</returns>
    public ParallelBlockBuilder<TContext> Execute<TStep>(string? nodeId = null)
        where TStep : class, IWorkflowStep<TContext>
    {
        var stepNode = new StepNode<TContext, TStep>(null, nodeId);
        _parallelNodes.Add(stepNode);
        return this;
    }

    /// <summary>
    /// Completes the parallel block and returns to the main workflow builder.
    /// </summary>
    /// <returns>The parent workflow builder</returns>
    public WorkflowBuilder<TContext> EndParallel()
    {
        var parallelNode = new ParallelNode<TContext>(_parallelNodes, null, _nodeId);
        _parentBuilder.LinkNodes(_branchPoint, parallelNode);
        _parentBuilder.SetCurrentNode(parallelNode);
        return _parentBuilder;
    }
}
