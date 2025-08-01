using DropBear.Codex.Workflow.Builder;
using DropBear.Codex.Workflow.Interfaces;

namespace DropBear.Codex.Workflow.Core;

/// <summary>
/// Abstract base class for implementing workflow definitions with a fluent interface.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public abstract class Workflow<TContext> : IWorkflowDefinition<TContext> where TContext : class
{
    private readonly Lazy<IWorkflowNode<TContext>> _workflowGraph;

    /// <summary>
    /// Initializes a new workflow definition.
    /// </summary>
    protected Workflow()
    {
        // Lazy initialization ensures the workflow is built only when needed
        _workflowGraph = new Lazy<IWorkflowNode<TContext>>(BuildWorkflowInternal);
    }

    /// <inheritdoc />
    public abstract string WorkflowId { get; }

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public virtual Version Version => new(1, 0);

    /// <inheritdoc />
    public virtual TimeSpan? WorkflowTimeout => null;

    /// <inheritdoc />
    public IWorkflowNode<TContext> BuildWorkflow() => _workflowGraph.Value;

    /// <summary>
    /// Override this method to configure the workflow structure using the fluent builder.
    /// </summary>
    /// <param name="builder">The workflow builder instance</param>
    protected abstract void Configure(WorkflowBuilder<TContext> builder);

    /// <summary>
    /// Internal method to build the workflow graph.
    /// </summary>
    private IWorkflowNode<TContext> BuildWorkflowInternal()
    {
        var builder = new WorkflowBuilder<TContext>(WorkflowId, DisplayName, Version);
        
        if (WorkflowTimeout.HasValue)
        {
            builder.WithTimeout(WorkflowTimeout.Value);
        }

        Configure(builder);
        var definition = builder.Build();
        return definition.BuildWorkflow();
    }
}
