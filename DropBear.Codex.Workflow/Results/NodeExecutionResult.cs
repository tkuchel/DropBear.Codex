namespace DropBear.Codex.Workflow.Results;

/// <summary>
/// Represents the result of a workflow node execution.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public readonly record struct NodeExecutionResult<TContext> where TContext : class
{
    /// <summary>
    /// The result of executing this node.
    /// </summary>
    public StepResult StepResult { get; init; }

    /// <summary>
    /// The next nodes to execute in the workflow.
    /// Empty collection indicates workflow completion.
    /// </summary>
    public IReadOnlyList<Interfaces.IWorkflowNode<TContext>> NextNodes { get; init; }

    /// <summary>
    /// Creates a successful node execution result.
    /// </summary>
    /// <param name="nextNodes">The next nodes to execute</param>
    /// <param name="metadata">Optional metadata</param>
    /// <returns>A successful node execution result</returns>
    public static NodeExecutionResult<TContext> Success(
        IReadOnlyList<Interfaces.IWorkflowNode<TContext>> nextNodes,
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new()
        {
            StepResult = StepResult.Success(metadata),
            NextNodes = nextNodes
        };

    /// <summary>
    /// Creates a failed node execution result.
    /// </summary>
    /// <param name="stepResult">The failed step result</param>
    /// <returns>A failed node execution result</returns>
    public static NodeExecutionResult<TContext> Failure(StepResult stepResult) =>
        new()
        {
            StepResult = stepResult,
            NextNodes = Array.Empty<Interfaces.IWorkflowNode<TContext>>()
        };

    /// <summary>
    /// Creates a completion result (no more nodes to execute).
    /// </summary>
    /// <param name="metadata">Optional metadata</param>
    /// <returns>A completion node execution result</returns>
    public static NodeExecutionResult<TContext> Complete(IReadOnlyDictionary<string, object>? metadata = null) =>
        new()
        {
            StepResult = StepResult.Success(metadata),
            NextNodes = Array.Empty<Interfaces.IWorkflowNode<TContext>>()
        };
}
