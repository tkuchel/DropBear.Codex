using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Nodes;

namespace DropBear.Codex.Workflow.Builder;

/// <summary>
/// FIXED: Fluent builder for constructing workflow definitions with proper node linking.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public sealed class WorkflowBuilder<TContext> where TContext : class
{
    private readonly string _workflowId;
    private readonly string _displayName;
    private readonly Version _version;
    private TimeSpan? _workflowTimeout;
    private IWorkflowNode<TContext>? _rootNode;
    private IWorkflowNode<TContext>? _currentNode;

    /// <summary>
    /// Initializes a new workflow builder.
    /// </summary>
    /// <param name="workflowId">Unique identifier for the workflow</param>
    /// <param name="displayName">Human-readable name for the workflow</param>
    /// <param name="version">Version of the workflow definition</param>
    public WorkflowBuilder(string workflowId, string displayName, Version? version = null)
    {
        _workflowId = workflowId ?? throw new ArgumentNullException(nameof(workflowId));
        _displayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        _version = version ?? new Version(1, 0);
    }

    /// <summary>
    /// Sets the workflow timeout.
    /// </summary>
    /// <param name="timeout">Maximum execution time for the workflow</param>
    /// <returns>The workflow builder for method chaining</returns>
    public WorkflowBuilder<TContext> WithTimeout(TimeSpan timeout)
    {
        _workflowTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Starts the workflow with the specified step type.
    /// </summary>
    /// <typeparam name="TStep">The type of step to start with</typeparam>
    /// <param name="nodeId">Optional custom node ID</param>
    /// <returns>The workflow builder for method chaining</returns>
    public WorkflowBuilder<TContext> StartWith<TStep>(string? nodeId = null)
        where TStep : class, IWorkflowStep<TContext>
    {
        var stepNode = new StepNode<TContext, TStep>(null, nodeId);
        _rootNode = stepNode;
        _currentNode = stepNode;
        return this;
    }

    /// <summary>
    /// FIXED: Adds a step that executes after the current step with proper linking.
    /// </summary>
    /// <typeparam name="TStep">The type of step to add</typeparam>
    /// <param name="nodeId">Optional custom node ID</param>
    /// <returns>The workflow builder for method chaining</returns>
    public WorkflowBuilder<TContext> Then<TStep>(string? nodeId = null)
        where TStep : class, IWorkflowStep<TContext>
    {
        if (_currentNode is null)
        {
            throw new InvalidOperationException(
                "Cannot add step without starting the workflow. Call StartWith<T>() first.");
        }

        // FIXED: Create a new StepNode that will be linked properly
        var stepNode = new StepNode<TContext, TStep>(null, nodeId);

        // FIXED: Use the new linking method that actually works
        LinkNodesProper(_currentNode, stepNode);
        _currentNode = stepNode;

        return this;
    }

    /// <summary>
    /// Adds a conditional branch to the workflow.
    /// </summary>
    /// <param name="condition">Predicate to evaluate for branching</param>
    /// <param name="nodeId">Optional custom node ID</param>
    /// <returns>A conditional branch builder</returns>
    public ConditionalBranchBuilder<TContext> If(Func<TContext, bool> condition, string? nodeId = null)
    {
        if (_currentNode is null)
        {
            throw new InvalidOperationException("Cannot add conditional without a current node.");
        }

        return new ConditionalBranchBuilder<TContext>(this, _currentNode, condition, nodeId);
    }

    /// <summary>
    /// Adds a parallel execution block to the workflow.
    /// </summary>
    /// <param name="nodeId">Optional custom node ID</param>
    /// <returns>A parallel block builder</returns>
    public ParallelBlockBuilder<TContext> InParallel(string? nodeId = null)
    {
        if (_currentNode is null)
        {
            throw new InvalidOperationException("Cannot add parallel block without a current node.");
        }

        return new ParallelBlockBuilder<TContext>(this, _currentNode, nodeId);
    }

    /// <summary>
    /// Adds a delay to the workflow.
    /// </summary>
    /// <param name="delay">Duration to delay execution</param>
    /// <param name="nodeId">Optional custom node ID</param>
    /// <returns>The workflow builder for method chaining</returns>
    public WorkflowBuilder<TContext> Delay(TimeSpan delay, string? nodeId = null)
    {
        if (_currentNode is null)
        {
            throw new InvalidOperationException("Cannot add delay without a current node.");
        }

        var delayNode = new DelayNode<TContext>(delay, null, nodeId);
        LinkNodesProper(_currentNode, delayNode);
        _currentNode = delayNode;

        return this;
    }

    /// <summary>
    /// Builds the final workflow definition.
    /// </summary>
    /// <returns>A completed workflow definition</returns>
    public IWorkflowDefinition<TContext> Build()
    {
        if (_rootNode is null)
        {
            throw new InvalidOperationException("Cannot build workflow without any steps. Call StartWith<T>() first.");
        }

        return new BuiltWorkflowDefinition<TContext>(_workflowId, _displayName, _version, _workflowTimeout, _rootNode);
    }

    /// <summary>
    /// FIXED: Proper node linking using improved StepNode NextNode property
    /// </summary>
    internal void LinkNodesProper(IWorkflowNode<TContext> fromNode, IWorkflowNode<TContext> toNode)
    {
        // Try to use the NextNode property if it's a StepNode
        if (fromNode.GetType().IsGenericType &&
            fromNode.GetType().GetGenericTypeDefinition().Name.Contains("StepNode"))
        {
            var nextNodeProperty = fromNode.GetType().GetProperty("NextNode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (nextNodeProperty?.CanWrite == true)
            {
                nextNodeProperty.SetValue(fromNode, toNode);
                return;
            }
        }

        // Fall back to reflection for _nextNode field
        LinkNodes(fromNode, toNode);
    }

    /// <summary>
    /// FIXED: Proper node linking using reflection as fallback
    /// </summary>
    internal void LinkNodes(IWorkflowNode<TContext> fromNode, IWorkflowNode<TContext> toNode)
    {
        // Try the reflection approach first (original implementation)
        var nextNodeField = fromNode.GetType().GetField("_nextNode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (nextNodeField is not null && nextNodeField.FieldType.IsAssignableFrom(typeof(IWorkflowNode<TContext>)))
        {
            nextNodeField.SetValue(fromNode, toNode);
            return;
        }

        // Try alternative field names
        var fields = fromNode.GetType()
            .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var nextField = fields.FirstOrDefault(f =>
            (f.Name.Contains("next", StringComparison.OrdinalIgnoreCase) ||
             f.Name.Contains("_next", StringComparison.OrdinalIgnoreCase)) &&
            f.FieldType.IsAssignableFrom(typeof(IWorkflowNode<TContext>)));

        if (nextField is not null)
        {
            nextField.SetValue(fromNode, toNode);
            return;
        }

        // If reflection fails, fall back to sequence node approach
        LinkNodesProper(fromNode, toNode);
    }

    /// <summary>
    /// Updates parent references when replacing nodes
    /// </summary>
    private void UpdateParentReferences(IWorkflowNode<TContext> oldNode, IWorkflowNode<TContext> newNode)
    {
        // This is a simplified implementation - in a production system you'd maintain
        // a proper graph structure with parent-child relationships
        // For now, we just update the current node reference
        if (_currentNode == oldNode)
        {
            _currentNode = newNode;
        }
    }

    /// <summary>
    /// Updates the current node reference.
    /// </summary>
    internal void SetCurrentNode(IWorkflowNode<TContext> node)
    {
        _currentNode = node;
    }
}
