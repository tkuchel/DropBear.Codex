namespace DropBear.Codex.Workflow.Interfaces;

/// <summary>
/// Main engine responsible for executing workflows.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Executes a workflow definition with the provided context.
    /// </summary>
    /// <typeparam name="TContext">The type of workflow context</typeparam>
    /// <param name="definition">The workflow definition to execute</param>
    /// <param name="context">The initial context state</param>
    /// <param name="cancellationToken">Token to cancel the execution</param>
    /// <returns>The final workflow execution result</returns>
    ValueTask<Results.WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class;

    /// <summary>
    /// Executes a workflow definition with the provided context and execution options.
    /// </summary>
    /// <typeparam name="TContext">The type of workflow context</typeparam>
    /// <param name="definition">The workflow definition to execute</param>
    /// <param name="context">The initial context state</param>
    /// <param name="options">Execution options and overrides</param>
    /// <param name="cancellationToken">Token to cancel the execution</param>
    /// <returns>The final workflow execution result</returns>
    ValueTask<Results.WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        Configuration.WorkflowExecutionOptions options,
        CancellationToken cancellationToken = default) where TContext : class;
}
