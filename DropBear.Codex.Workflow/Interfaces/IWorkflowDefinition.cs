namespace DropBear.Codex.Workflow.Interfaces;

/// <summary>
/// Defines the structure and execution logic of a workflow.
/// </summary>
/// <typeparam name="TContext">The type of context that flows through the workflow</typeparam>
public interface IWorkflowDefinition<TContext> where TContext : class
{
    /// <summary>
    /// Gets the unique identifier for this workflow definition.
    /// </summary>
    string WorkflowId { get; }

    /// <summary>
    /// Gets the human-readable name of this workflow.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the version of this workflow definition.
    /// Used for versioning and migration scenarios.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Gets the maximum execution time for the entire workflow.
    /// Returns null for no timeout.
    /// </summary>
    TimeSpan? WorkflowTimeout { get; }

    /// <summary>
    /// Builds the workflow execution graph.
    /// </summary>
    /// <returns>The root node of the workflow execution graph</returns>
    IWorkflowNode<TContext> BuildWorkflow();
}
