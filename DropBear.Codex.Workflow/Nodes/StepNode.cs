using Microsoft.Extensions.DependencyInjection;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
/// IMPROVED: Workflow node that executes a single step with proper next node handling.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
/// <typeparam name="TStep">The type of step to execute</typeparam>
public sealed class StepNode<TContext, TStep> : WorkflowNodeBase<TContext>
    where TContext : class
    where TStep : class, IWorkflowStep<TContext>
{
    private IWorkflowNode<TContext>? _nextNode;
    private readonly string _nodeId;

    /// <summary>
    /// Initializes a new step node.
    /// </summary>
    /// <param name="nextNode">The next node to execute after this step</param>
    /// <param name="nodeId">Optional custom node ID</param>
    public StepNode(IWorkflowNode<TContext>? nextNode = null, string? nodeId = null)
    {
        _nextNode = nextNode;
        _nodeId = nodeId ?? CreateNodeId();
    }

    /// <inheritdoc />
    public override string NodeId => _nodeId;

    /// <summary>
    /// IMPROVED: Property to access and set the next node (used by WorkflowBuilder).
    /// </summary>
    internal IWorkflowNode<TContext>? NextNode
    {
        get => _nextNode;
        set => _nextNode = value;
    }

    /// <inheritdoc />
    public override async ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        // Resolve the step from DI container
        var step = serviceProvider.GetRequiredService<TStep>();

        try
        {
            // Execute the step with timeout if specified
            var stepResult = step.Timeout.HasValue
                ? await ExecuteWithTimeout(step, context, step.Timeout.Value, cancellationToken)
                : await step.ExecuteAsync(context, cancellationToken);

            // CRITICAL: Check for suspension signals first - these should NOT proceed to next nodes
            if (!stepResult.IsSuccess && IsSuspensionSignal(stepResult))
            {
                // Return suspension signal immediately without next nodes
                return NodeExecutionResult<TContext>.Failure(stepResult);
            }

            // Determine next nodes based on execution result
            var nextNodes = stepResult.IsSuccess && _nextNode is not null
                ? new[] { _nextNode }
                : Array.Empty<IWorkflowNode<TContext>>();

            return stepResult.IsSuccess
                ? NodeExecutionResult<TContext>.Success(nextNodes, stepResult.Metadata)
                : NodeExecutionResult<TContext>.Failure(stepResult);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not considered a failure
            throw;
        }
        catch (Exception ex)
        {
            // Wrap unexpected exceptions
            var stepResult = StepResult.Failure(ex, step.CanRetry);
            return NodeExecutionResult<TContext>.Failure(stepResult);
        }
    }

    /// <summary>
    /// Executes a step with the specified timeout.
    /// </summary>
    private static async ValueTask<StepResult> ExecuteWithTimeout(
        IWorkflowStep<TContext> step,
        TContext context,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await step.ExecuteAsync(context, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested &&
                                                 !cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred
            return StepResult.Failure($"Step '{step.StepName}' timed out after {timeout}", step.CanRetry);
        }
    }

    /// <summary>
    /// Checks if a step result represents a suspension signal.
    /// </summary>
    private static bool IsSuspensionSignal(StepResult stepResult)
    {
        return stepResult.ErrorMessage?.StartsWith("WAITING_FOR_SIGNAL:", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Gets the step type name for debugging.
    /// </summary>
    public string StepTypeName => typeof(TStep).Name;
}
