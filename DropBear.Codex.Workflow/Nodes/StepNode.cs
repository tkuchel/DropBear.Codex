#region

using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Metrics;
using DropBear.Codex.Workflow.Results;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
///     Workflow node that executes a single step with retry logic and timeout support.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
/// <typeparam name="TStep">The type of workflow step to execute</typeparam>
public sealed class StepNode<TContext, TStep> : WorkflowNodeBase<TContext>, ILinkableNode<TContext>
    where TContext : class
    where TStep : class, IWorkflowStep<TContext>
{
    private IWorkflowNode<TContext>? _nextNode;

    /// <summary>
    ///     Initializes a new step node.
    /// </summary>
    /// <param name="nextNode">The next node in the workflow</param>
    /// <param name="nodeId">Optional custom node identifier</param>
    public StepNode(IWorkflowNode<TContext>? nextNode = null, string? nodeId = null)
    {
        _nextNode = nextNode;
        NodeId = nodeId ?? CreateNodeId();
    }

    /// <inheritdoc />
    public override string NodeId { get; }

    /// <summary>
    ///     Gets the name of the step type.
    /// </summary>
    public string StepTypeName => typeof(TStep).Name;

    /// <inheritdoc />
    public void SetNextNode(IWorkflowNode<TContext>? nextNode) => _nextNode = nextNode;

    /// <inheritdoc />
    public IWorkflowNode<TContext>? GetNextNode() => _nextNode;

    /// <inheritdoc />
    public override async ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        TStep step = serviceProvider.GetRequiredService<TStep>();
        DateTimeOffset startTime = DateTimeOffset.UtcNow;
        int retryAttempts = 0;
        StepResult lastResult = StepResult.Failure("Step execution did not complete");

        RetryPolicy retryPolicy = RetryPolicy.Default;

        try
        {
            int maxAttempts = step.CanRetry ? retryPolicy.MaxAttempts : 1;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (attempt > 0)
                {
                    retryAttempts = attempt;
                    TimeSpan delay = CalculateRetryDelay(attempt, retryPolicy);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                lastResult = step.Timeout.HasValue
                    ? await ExecuteWithTimeoutAsync(step, context, step.Timeout.Value, cancellationToken)
                        .ConfigureAwait(false)
                    : await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

                if (lastResult.IsSuccess)
                {
                    break;
                }

                if (!lastResult.ShouldRetry || !step.CanRetry)
                {
                    break;
                }

                if (retryPolicy.ShouldRetryPredicate != null &&
                    lastResult.Error?.SourceException is { } sourceException &&
                    !retryPolicy.ShouldRetryPredicate(sourceException))
                {
                    break;
                }
            }

            DateTimeOffset endTime = DateTimeOffset.UtcNow;

            // ===== UPDATED: Add StepType and ContextType to trace =====
            var trace = new StepExecutionTrace
            {
                StepName = step.StepName,
                NodeId = NodeId,
                StartTime = startTime,
                EndTime = endTime,
                Result = lastResult,
                RetryAttempts = retryAttempts,
                StepType = typeof(TStep), // NEW: Store step type
                ContextType = typeof(TContext) // NEW: Store context type
            };
            // ==========================================================

            if (lastResult is not { IsSuccess: true } &&
                lastResult.Error?.Message is { } errorMessage &&
                WorkflowConstants.Signals.IsSuspensionSignal(errorMessage))
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
            var failureResult = StepResult.Failure(ex);

            // ===== UPDATED: Add StepType and ContextType to trace =====
            var trace = new StepExecutionTrace
            {
                StepName = step.StepName,
                NodeId = NodeId,
                StartTime = startTime,
                EndTime = endTime,
                Result = failureResult,
                RetryAttempts = retryAttempts,
                StepType = typeof(TStep), // NEW: Store step type
                ContextType = typeof(TContext) // NEW: Store context type
            };
            // ==========================================================

            return NodeExecutionResult<TContext>.Failure(failureResult, trace);
        }
    }

    /// <summary>
    ///     Calculates the retry delay using exponential backoff.
    /// </summary>
    private static TimeSpan CalculateRetryDelay(int attempt, RetryPolicy policy)
    {
        double delayMs = policy.BaseDelay.TotalMilliseconds * Math.Pow(policy.BackoffMultiplier, attempt - 1);
        var calculatedDelay = TimeSpan.FromMilliseconds(delayMs);

        return calculatedDelay > policy.MaxDelay ? policy.MaxDelay : calculatedDelay;
    }

    /// <summary>
    ///     Executes a step with a timeout.
    /// </summary>
    private static async ValueTask<StepResult> ExecuteWithTimeoutAsync(
        TStep step,
        TContext context,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await step.ExecuteAsync(context, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StepResult.Failure($"Step '{step.StepName}' exceeded timeout of {timeout.TotalSeconds} seconds");
        }
    }
}
