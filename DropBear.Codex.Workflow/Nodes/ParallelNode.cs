#region

using DropBear.Codex.Workflow.Common;
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
            Task<NodeExecutionResult<TContext>>[] tasks = ParallelNodes
                .Select(node => ExecuteNodeAsync(node, context, serviceProvider, cancellationToken))
                .ToArray();

            NodeExecutionResult<TContext>[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
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
