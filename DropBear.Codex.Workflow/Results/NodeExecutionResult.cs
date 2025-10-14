#region

using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Metrics;

#endregion

namespace DropBear.Codex.Workflow.Results;

/// <summary>
///     Represents the result of a workflow node execution.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public readonly record struct NodeExecutionResult<TContext> where TContext : class
{
    /// <summary>
    ///     The result of executing this node.
    /// </summary>
    public StepResult StepResult { get; init; }

    /// <summary>
    ///     The next nodes to execute in the workflow.
    /// </summary>
    public IReadOnlyList<IWorkflowNode<TContext>> NextNodes { get; init; }

    /// <summary>
    ///     The step execution trace for this node (if it was a step node).
    /// </summary>
    public StepExecutionTrace? StepTrace { get; init; }

    /// <summary>
    ///     Creates a successful node execution result.
    /// </summary>
    public static NodeExecutionResult<TContext> Success(
        IReadOnlyList<IWorkflowNode<TContext>> nextNodes,
        IReadOnlyDictionary<string, object>? metadata = null,
        StepExecutionTrace? stepTrace = null) =>
        new() { StepResult = StepResult.Success(metadata), NextNodes = nextNodes, StepTrace = stepTrace };

    /// <summary>
    ///     Creates a failed node execution result.
    /// </summary>
    public static NodeExecutionResult<TContext> Failure(
        StepResult stepResult,
        StepExecutionTrace? stepTrace = null) =>
        new() { StepResult = stepResult, NextNodes = Array.Empty<IWorkflowNode<TContext>>(), StepTrace = stepTrace };

    /// <summary>
    ///     Creates a completion result (no more nodes to execute).
    /// </summary>
    public static NodeExecutionResult<TContext> Complete(
        IReadOnlyDictionary<string, object>? metadata = null,
        StepExecutionTrace? stepTrace = null) =>
        new()
        {
            StepResult = StepResult.Success(metadata),
            NextNodes = Array.Empty<IWorkflowNode<TContext>>(),
            StepTrace = stepTrace
        };
}
