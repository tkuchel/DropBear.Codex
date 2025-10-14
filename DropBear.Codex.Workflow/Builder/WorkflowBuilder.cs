#region

using System.Text.RegularExpressions;
using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Exceptions;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Nodes;

#endregion

namespace DropBear.Codex.Workflow.Builder;

/// <summary>
///     Fluent builder for constructing workflow definitions with explicit node linking.
/// </summary>
public sealed partial class WorkflowBuilder<TContext> where TContext : class
{
    private readonly string _displayName;
    private readonly Version _version;
    private IWorkflowNode<TContext>? _currentNode;
    private IWorkflowNode<TContext>? _rootNode;
    private TimeSpan? _workflowTimeout;

    public WorkflowBuilder(string workflowId, string displayName, Version? version = null)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            throw new ArgumentException("Workflow ID cannot be null or whitespace.", nameof(workflowId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be null or whitespace.", nameof(displayName));
        }

        if (!IsValidWorkflowId(workflowId))
        {
            throw new ArgumentException(
                "Workflow ID must contain only alphanumeric characters, hyphens, underscores, and periods.",
                nameof(workflowId));
        }

        if (workflowId.Length > WorkflowConstants.Limits.MaxWorkflowIdLength)
        {
            throw new ArgumentException(
                $"Workflow ID cannot exceed {WorkflowConstants.Limits.MaxWorkflowIdLength} characters.",
                nameof(workflowId));
        }

        WorkflowId = workflowId;
        _displayName = displayName;
        _version = version ?? WorkflowConstants.Defaults.DefaultVersion;
    }

    internal string? WorkflowId { get; }

    [GeneratedRegex(@"^[a-zA-Z0-9\-_\.]+$", RegexOptions.Compiled, 1000)]
    private static partial Regex WorkflowIdPattern();

    /// <summary>
    ///     Sets the workflow timeout.
    /// </summary>
    public WorkflowBuilder<TContext> WithTimeout(TimeSpan timeout)
    {
        if (timeout < WorkflowConstants.Limits.MinWorkflowTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                $"Timeout must be at least {WorkflowConstants.Limits.MinWorkflowTimeout}.");
        }

        if (timeout > WorkflowConstants.Limits.MaxWorkflowTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                $"Timeout must not exceed {WorkflowConstants.Limits.MaxWorkflowTimeout}.");
        }

        _workflowTimeout = timeout;
        return this;
    }

    /// <summary>
    ///     Starts the workflow with a step.
    /// </summary>
    public WorkflowBuilder<TContext> StartWith<TStep>(string? nodeId = null)
        where TStep : class, IWorkflowStep<TContext>
    {
        if (_rootNode is not null)
        {
            throw new InvalidOperationException("Workflow already has a starting step.");
        }

        var stepNode = new StepNode<TContext, TStep>(null, nodeId);
        _rootNode = stepNode;
        _currentNode = stepNode;

        return this;
    }

    /// <summary>
    ///     Adds a step to the workflow.
    /// </summary>
    public WorkflowBuilder<TContext> Then<TStep>(string? nodeId = null)
        where TStep : class, IWorkflowStep<TContext>
    {
        if (_currentNode is null)
        {
            throw new InvalidOperationException(
                "Cannot add step without starting the workflow. Call StartWith<T>() first.");
        }

        var stepNode = new StepNode<TContext, TStep>(null, nodeId);
        LinkNodes(_currentNode, stepNode);
        _currentNode = stepNode;

        return this;
    }

    /// <summary>
    ///     Adds a conditional branch.
    /// </summary>
    public ConditionalBranchBuilder<TContext> If(Func<TContext, bool> condition, string? nodeId = null)
    {
        if (_currentNode is null)
        {
            throw new InvalidOperationException(
                "Cannot add conditional without a current node. Call StartWith<T>() first.");
        }

        ArgumentNullException.ThrowIfNull(condition);

        return new ConditionalBranchBuilder<TContext>(this, _currentNode, condition, nodeId);
    }

    /// <summary>
    ///     Adds a parallel execution block.
    /// </summary>
    public ParallelBlockBuilder<TContext> InParallel(string? nodeId = null)
    {
        if (_currentNode is null)
        {
            throw new InvalidOperationException(
                "Cannot add parallel block without a current node. Call StartWith<T>() first.");
        }

        return new ParallelBlockBuilder<TContext>(this, _currentNode, nodeId);
    }

    /// <summary>
    ///     Adds a delay to the workflow.
    /// </summary>
    public WorkflowBuilder<TContext> Delay(TimeSpan delay, string? nodeId = null)
    {
        if (_currentNode is null)
        {
            throw new InvalidOperationException("Cannot add delay without a current node. Call StartWith<T>() first.");
        }

        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay must be greater than zero.");
        }

        if (delay > WorkflowConstants.Limits.MaxStepTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delay),
                delay,
                $"Delay must not exceed {WorkflowConstants.Limits.MaxStepTimeout}.");
        }

        var delayNode = new DelayNode<TContext>(delay, null, nodeId);
        LinkNodes(_currentNode, delayNode);
        _currentNode = delayNode;

        return this;
    }

    /// <summary>
    ///     Builds the final workflow definition.
    /// </summary>
    public IWorkflowDefinition<TContext> Build()
    {
        if (_rootNode is null)
        {
            throw new InvalidOperationException("Cannot build workflow without any steps. Call StartWith<T>() first.");
        }

        return new BuiltWorkflowDefinition<TContext>(WorkflowId!, _displayName, _version, _workflowTimeout, _rootNode);
    }

    internal void LinkNodes(IWorkflowNode<TContext> fromNode, IWorkflowNode<TContext> toNode)
    {
        ArgumentNullException.ThrowIfNull(fromNode, nameof(fromNode));
        ArgumentNullException.ThrowIfNull(toNode, nameof(toNode));

        if (fromNode is ILinkableNode<TContext> linkableNode)
        {
            linkableNode.SetNextNode(toNode);
            return;
        }

        throw new WorkflowConfigurationException(
            $"Node of type '{fromNode.GetType().Name}' does not support sequential linking. " +
            $"Nodes must implement ILinkableNode<{typeof(TContext).Name}> to be linked in sequence.",
            WorkflowId);
    }

    internal void SetCurrentNode(IWorkflowNode<TContext> node)
    {
        ArgumentNullException.ThrowIfNull(node, nameof(node));
        _currentNode = node;
    }

    private static bool IsValidWorkflowId(string workflowId) => WorkflowIdPattern().IsMatch(workflowId);
}
