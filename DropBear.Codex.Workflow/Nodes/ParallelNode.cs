#region

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DropBear.Codex.Core.Results.Async;
using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Context;
using DropBear.Codex.Workflow.Errors;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
///     Workflow node that executes multiple child nodes in parallel.
/// </summary>
public sealed class ParallelNode<TContext> : WorkflowNodeBase<TContext>, ILinkableNode<TContext>
    where TContext : class
{
    private IWorkflowNode<TContext>? _nextNode;

    public ParallelNode(
        IReadOnlyList<IWorkflowNode<TContext>> parallelNodes,
        IWorkflowNode<TContext>? nextNode = null,
        string? nodeId = null)
    {
        ArgumentNullException.ThrowIfNull(parallelNodes);

        if (parallelNodes.Count == 0)
        {
            throw new ArgumentException("Parallel node must have at least one child node.", nameof(parallelNodes));
        }

        if (parallelNodes.Count > WorkflowConstants.Limits.MaxParallelBranches)
        {
            throw new ArgumentException(
                $"Parallel node cannot exceed {WorkflowConstants.Limits.MaxParallelBranches} branches. Found {parallelNodes.Count} branches.",
                nameof(parallelNodes));
        }

        ParallelNodes = parallelNodes;
        _nextNode = nextNode;
        NodeId = nodeId ?? CreateNodeId();
    }

    public override string NodeId { get; }

    public int BranchCount => ParallelNodes.Count;

    public IReadOnlyList<IWorkflowNode<TContext>> ParallelNodes { get; }

    public void SetNextNode(IWorkflowNode<TContext>? nextNode) => _nextNode = nextNode;

    public IWorkflowNode<TContext>? GetNextNode() => _nextNode;

    public override async ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (ParallelNodes.Count == 0)
        {
            IWorkflowNode<TContext>[] nextNodes =
                _nextNode is not null ? [_nextNode] : [];
            return NodeExecutionResult<TContext>.Success(nextNodes);
        }

        DateTimeOffset startTime = DateTimeOffset.UtcNow;

        try
        {
            // QUALITY FIX: Use configured max degree of parallelism from execution options if available
            int maxDegreeOfParallelism = GetMaxDegreeOfParallelism(serviceProvider);

            NodeExecutionResult<TContext>[] results = await ExecuteParallelWithThrottlingAsync(
                ParallelNodes,
                maxDegreeOfParallelism,
                context,
                serviceProvider,
                cancellationToken).ConfigureAwait(false);

            DateTimeOffset endTime = DateTimeOffset.UtcNow;

            NodeExecutionResult<TContext>[] failedResults = results.Where(r => !r.StepResult.IsSuccess).ToArray();
            if (failedResults.Length > 0)
            {
                NodeExecutionResult<TContext> firstFailure = failedResults[0];
                int failedCount = failedResults.Length;

                string? errorMessage = failedCount == 1
                    ? firstFailure.StepResult.Error?.Message
                    : $"{failedCount} parallel branches failed. First error: {firstFailure.StepResult.Error?.Message}";

                var aggregatedResult = StepResult.Failure(
                    errorMessage ?? "Parallel execution failed",
                    firstFailure.StepResult.ShouldRetry);

                return NodeExecutionResult<TContext>.Failure(aggregatedResult);
            }

            var allNextNodes = results.SelectMany(r => r.NextNodes).ToList();

            if (_nextNode is not null)
            {
                allNextNodes.Add(_nextNode);
            }

            var metadata = new Dictionary<string, object>
                (StringComparer.OrdinalIgnoreCase)
                {
                    ["ParallelBranches"] = ParallelNodes.Count,
                    ["SuccessfulBranches"] = ParallelNodes.Count,
                    ["FailedBranches"] = 0,
                    ["MaxDegreeOfParallelism"] = maxDegreeOfParallelism,
                    ["ExecutionTime"] = (endTime - startTime).TotalMilliseconds
                };

            return NodeExecutionResult<TContext>.Success(allNextNodes, metadata);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var stepResult = StepResult.Failure($"Parallel node execution failed: {ex.Message}");
            return NodeExecutionResult<TContext>.Failure(stepResult);
        }
    }

    /// <summary>
    ///     Streams parallel node execution results as they complete, rather than waiting for all branches to finish.
    ///     This allows consumers to react to branch completions in real-time.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///     An <see cref="AsyncEnumerableResult{T, TError}" /> that yields <see cref="NodeExecutionResult{TContext}" />
    ///     instances as parallel branches complete execution.
    /// </returns>
    /// <remarks>
    ///     This method is significantly more responsive than <see cref="ExecuteAsync" /> when dealing with
    ///     many parallel branches. Results are yielded immediately upon completion, enabling real-time
    ///     progress tracking and early failure detection. Execution respects the configured degree of parallelism.
    /// </remarks>
    public AsyncEnumerableResult<NodeExecutionResult<TContext>, WorkflowExecutionError> StreamParallelResultsAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (ParallelNodes.Count == 0)
        {
            // Return empty enumerable for no parallel nodes
            async IAsyncEnumerable<NodeExecutionResult<TContext>> EmptyResults(
                [EnumeratorCancellation] CancellationToken ct = default)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                yield break;
            }

            return AsyncEnumerableResult<NodeExecutionResult<TContext>, WorkflowExecutionError>
                .Success(EmptyResults(cancellationToken));
        }

        async IAsyncEnumerable<NodeExecutionResult<TContext>> StreamInternal(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            // SECURITY: Use bounded channel to prevent memory exhaustion
            int capacity = ParallelNodes.Count * 2;
            var channel = Channel.CreateBounded<NodeExecutionResult<TContext>>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false // Multiple parallel tasks write to this channel
            });

            // QUALITY FIX: Use configured max degree of parallelism from execution options if available
            int maxDegreeOfParallelism = GetMaxDegreeOfParallelism(serviceProvider);
            using var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);

            // Start all parallel executions
            _ = Task.Run(async () =>
            {
                var tasks = ParallelNodes.Select(async node =>
                {
                    await semaphore.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        var result = await ExecuteNodeAsync(node, context, serviceProvider, ct)
                            .ConfigureAwait(false);

                        // Write result to channel as soon as it's available
                        await channel.Writer.WriteAsync(result, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToArray();

                // Wait for all to complete
                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                finally
                {
                    // Signal completion
                    channel.Writer.Complete();
                }
            }, ct);

            // Yield results as they arrive
            await foreach (var result in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return result;
            }
        }

        return AsyncEnumerableResult<NodeExecutionResult<TContext>, WorkflowExecutionError>
            .Success(StreamInternal(cancellationToken));
    }

    /// <summary>
    ///     Executes parallel nodes with throttling to limit concurrency.
    ///     QUALITY FIX: Properly propagates cancellation to sibling tasks when one fails or is cancelled.
    /// </summary>
    /// <param name="nodes">The nodes to execute in parallel</param>
    /// <param name="maxDegreeOfParallelism">Maximum number of concurrent executions</param>
    /// <param name="context">The workflow context</param>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of execution results</returns>
    private static async ValueTask<NodeExecutionResult<TContext>[]> ExecuteParallelWithThrottlingAsync(
        IReadOnlyList<IWorkflowNode<TContext>> nodes,
        int maxDegreeOfParallelism,
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // Use semaphore to throttle concurrent execution
        using var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);

        // Create a linked cancellation source to cancel siblings when any task fails
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var results = new NodeExecutionResult<TContext>[nodes.Count];
        var hasFailure = false;

        var tasks = nodes.Select(async (node, index) =>
        {
            await semaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            try
            {
                var result = await ExecuteNodeAsync(node, context, serviceProvider, linkedCts.Token)
                    .ConfigureAwait(false);

                results[index] = result;

                // If this task failed and we haven't already triggered cancellation, do so now
                if (!result.StepResult.IsSuccess && !hasFailure)
                {
                    hasFailure = true;
                    await linkedCts.CancelAsync().ConfigureAwait(false);
                }

                return result;
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Sibling task was cancelled due to another task's failure
                // Return a failure result instead of propagating the exception
                var failureResult = NodeExecutionResult<TContext>.Failure(
                    StepResult.Failure("Task cancelled due to sibling task failure"));
                results[index] = failureResult;
                return failureResult;
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Some tasks were cancelled due to sibling failure - this is expected
            // Results array already contains the failure information
        }

        return results;
    }

    private static async Task<NodeExecutionResult<TContext>> ExecuteNodeAsync(
        IWorkflowNode<TContext> node,
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            return await node.ExecuteAsync(context, serviceProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return NodeExecutionResult<TContext>.Failure(StepResult.Failure(ex));
        }
    }

    /// <summary>
    ///     Gets the maximum degree of parallelism from execution options if available,
    ///     otherwise returns the default value.
    /// </summary>
    private static int GetMaxDegreeOfParallelism(IServiceProvider serviceProvider)
    {
        var executionContext = serviceProvider.GetService<IWorkflowExecutionContext>();
        return executionContext?.Options?.MaxDegreeOfParallelism
               ?? WorkflowConstants.Defaults.DefaultMaxDegreeOfParallelism;
    }
}
