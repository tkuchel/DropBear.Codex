#region

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DropBear.Codex.Core.Results.Async;
using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Errors;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

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
            // Execute parallel nodes with throttling to prevent resource exhaustion
            int maxDegreeOfParallelism = WorkflowConstants.Defaults.DefaultMaxDegreeOfParallelism;
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
            // Use unbounded channel for result collection
            var channel = Channel.CreateUnbounded<NodeExecutionResult<TContext>>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false // Multiple parallel tasks write to this channel
            });

            int maxDegreeOfParallelism = WorkflowConstants.Defaults.DefaultMaxDegreeOfParallelism;
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

        var tasks = nodes.Select(async node =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await ExecuteNodeAsync(node, context, serviceProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
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
}
