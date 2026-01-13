#region

using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Nodes;

#endregion

namespace DropBear.Codex.Workflow.Builder;

/// <summary>
///     IMPROVED: Builder for parallel execution blocks in workflows.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public sealed class ParallelBlockBuilder<TContext> where TContext : class
{
    private readonly IWorkflowNode<TContext> _branchPoint;
    private readonly string? _nodeId;
    private readonly List<IWorkflowNode<TContext>> _parallelNodes = new();
    private readonly WorkflowBuilder<TContext> _parentBuilder;

    /// <summary>
    ///     Initializes a new parallel block builder.
    /// </summary>
    /// <param name="parentBuilder">The parent workflow builder</param>
    /// <param name="branchPoint">The node where parallel execution begins</param>
    /// <param name="nodeId">Optional custom node ID</param>
    internal ParallelBlockBuilder(
        WorkflowBuilder<TContext> parentBuilder,
        IWorkflowNode<TContext> branchPoint,
        string? nodeId)
    {
        _parentBuilder = parentBuilder ?? throw new ArgumentNullException(nameof(parentBuilder));
        _branchPoint = branchPoint ?? throw new ArgumentNullException(nameof(branchPoint));
        _nodeId = nodeId;
    }

    /// <summary>
    ///     Gets the number of parallel branches configured.
    /// </summary>
    public int BranchCount => _parallelNodes.Count;

    /// <summary>
    ///     Adds a step to execute in parallel.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute in parallel</typeparam>
    /// <param name="nodeId">Optional custom node ID</param>
    /// <returns>The parallel block builder for method chaining</returns>
    public ParallelBlockBuilder<TContext> Execute<TStep>(string? nodeId = null)
        where TStep : class, IWorkflowStep<TContext>
    {
        if (_parallelNodes.Count >= WorkflowConstants.Limits.MaxParallelBranches)
        {
            throw new InvalidOperationException(
                $"Cannot add more than {WorkflowConstants.Limits.MaxParallelBranches} parallel branches.");
        }

        var stepNode = new StepNode<TContext, TStep>(null, nodeId);
        _parallelNodes.Add(stepNode);
        return this;
    }

    /// <summary>
    ///     Adds multiple steps to execute in parallel.
    /// </summary>
    /// <param name="stepTypes">The types of steps to execute in parallel</param>
    /// <returns>The parallel block builder for method chaining</returns>
    /// <remarks>
    ///     Uses params ReadOnlySpan for zero-allocation when called with inline arguments.
    /// </remarks>
    public ParallelBlockBuilder<TContext> ExecuteAll(params ReadOnlySpan<Type> stepTypes)
    {
        foreach (var stepType in stepTypes)
        {
            if (!typeof(IWorkflowStep<TContext>).IsAssignableFrom(stepType))
            {
                throw new ArgumentException(
                    $"Type {stepType.Name} does not implement IWorkflowStep<{typeof(TContext).Name}>",
                    nameof(stepTypes));
            }

            if (_parallelNodes.Count >= WorkflowConstants.Limits.MaxParallelBranches)
            {
                throw new InvalidOperationException(
                    $"Cannot add more than {WorkflowConstants.Limits.MaxParallelBranches} parallel branches.");
            }

            // Create StepNode using reflection (only way to dynamically create generic types)
            Type stepNodeType = typeof(StepNode<,>).MakeGenericType(typeof(TContext), stepType);
            var stepNode = (IWorkflowNode<TContext>)Activator.CreateInstance(
                stepNodeType,
                null,
                null)!;

            _parallelNodes.Add(stepNode);
        }

        return this;
    }

    /// <summary>
    ///     Adds a custom node to execute in parallel.
    /// </summary>
    /// <param name="node">The node to execute in parallel. Must not be null.</param>
    /// <returns>The parallel block builder for method chaining</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when node is null
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when maximum parallel branches limit is exceeded
    /// </exception>
    public ParallelBlockBuilder<TContext> ExecuteNode(IWorkflowNode<TContext> node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (_parallelNodes.Count >= WorkflowConstants.Limits.MaxParallelBranches)
        {
            throw new InvalidOperationException(
                $"Cannot add more than {WorkflowConstants.Limits.MaxParallelBranches} parallel branches.");
        }

        _parallelNodes.Add(node);
        return this;
    }

    /// <summary>
    ///     Completes the parallel block and returns to the main workflow builder.
    /// </summary>
    /// <returns>The parent workflow builder</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when no parallel branches have been configured
    /// </exception>
    public WorkflowBuilder<TContext> EndParallel()
    {
        if (_parallelNodes.Count == 0)
        {
            throw new InvalidOperationException(
                "Parallel block must have at least one branch. Call Execute<TStep>() to add branches.");
        }

        // Create the parallel node with all branches
        var parallelNode = new ParallelNode<TContext>(_parallelNodes, null, _nodeId);

        // Link the branch point to the parallel node
        _parentBuilder.LinkNodes(_branchPoint, parallelNode);

        // Set the parallel node as the current node
        _parentBuilder.SetCurrentNode(parallelNode);

        return _parentBuilder;
    }
}
