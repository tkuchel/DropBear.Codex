#region

using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Metrics;
using DropBear.Codex.Workflow.Results;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
///     Workflow node that executes a single step with retry logic and timeout support.
/// </summary>
public sealed class StepNode<TContext, TStep> : WorkflowNodeBase<TContext>, ILinkableNode<TContext>
    where TContext : class
    where TStep : class, IWorkflowStep<TContext>
{
    private IWorkflowNode<TContext>? _nextNode;

    public StepNode(IWorkflowNode<TContext>? nextNode = null, string? nodeId = null)
    {
        _nextNode = nextNode;
        NodeId = nodeId ?? CreateNodeId();
    }

    public override string NodeId { get; }

    public string StepTypeName => typeof(TStep).Name;

    public void SetNextNode(IWorkflowNode<TContext>? nextNode) => _nextNode = nextNode;

    public IWorkflowNode<TContext>? GetNextNode() => _nextNode;

    public override async ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        TStep step = serviceProvider.GetRequiredService<TStep>();
        DateTimeOffset startTime = DateTimeOffset.UtcNow;
        int retryAttempts = 0;
        StepResult? lastResult = null;

        try
        {
            // Execute with retry logic
            for (int attempt = 0; attempt <= 3; attempt++)
            {
                if (attempt > 0)
                {
                    retryAttempts = attempt;
                    var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1));
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                lastResult = step.Timeout.HasValue
                    ? await ExecuteWithTimeoutAsync(step, context, step.Timeout.Value, cancellationToken)
                        .ConfigureAwait(false)
                    : await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

                if (lastResult.IsSuccess || !lastResult.ShouldRetry || !step.CanRetry)
                {
                    break;
                }
            }

            DateTimeOffset endTime = DateTimeOffset.UtcNow;
            var trace = new StepExecutionTrace
            {
                StepName = step.StepName,
                NodeId = NodeId,
                StartTime = startTime,
                EndTime = endTime,
                Result = lastResult,
                RetryAttempts = retryAttempts
            };

            // Check for suspension
            if (!lastResult.IsSuccess && WorkflowConstants.Signals.IsSuspensionSignal(lastResult.Error?.Message))
            {
                return NodeExecutionResult<TContext>.Failure(lastResult, trace);
            }

            IWorkflowNode<TContext>[] nextNodes = lastResult.IsSuccess && _nextNode is not null
                ? [_nextNode]
                : [];

            return lastResult.IsSuccess
                ? NodeExecutionResult<TContext>.Success(nextNodes, lastResult.Metadata, trace)
                : NodeExecutionResult<TContext>.Failure(lastResult, trace);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            DateTimeOffset endTime = DateTimeOffset.UtcNow;
            var stepResult = StepResult.Failure(ex, step.CanRetry);
            var trace = new StepExecutionTrace
            {
                StepName = step.StepName,
                NodeId = NodeId,
                StartTime = startTime,
                EndTime = endTime,
                Result = stepResult,
                RetryAttempts = retryAttempts
            };

            return NodeExecutionResult<TContext>.Failure(stepResult, trace);
        }
    }

    private static async ValueTask<StepResult> ExecuteWithTimeoutAsync(
        IWorkflowStep<TContext> step,
        TContext context,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await step.ExecuteAsync(context, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested &&
                                                 !cancellationToken.IsCancellationRequested)
        {
            return StepResult.Failure($"Step '{step.StepName}' timed out after {timeout}", step.CanRetry);
        }
    }
}
