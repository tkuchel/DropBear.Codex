#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;


public sealed class ParallelTaskScheduler : IDisposable
{
    private readonly ILogger _logger;
    private readonly int _maxDegreeOfParallelism;
    private readonly ConcurrentDictionary<string, TaskExecutionMetrics> _metrics;
    private readonly TaskExecutionStats _stats;
    private readonly SemaphoreSlim _throttle;
    private bool _disposed;

    public ParallelTaskScheduler(
        int maxDegreeOfParallelism,
        TaskExecutionStats stats,
        ConcurrentDictionary<string, TaskExecutionMetrics> metrics)
    {
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = LoggerFactory.Logger.ForContext<ParallelTaskScheduler>();
        _throttle = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _throttle.Dispose();
    }

    public async Task<Result<Unit, TaskExecutionError>> ExecuteTasksAsync(
        TaskPriorityQueue taskQueue,
        TaskExecutionScope scope,
        CancellationToken cancellationToken)
    {
        var failedTasks = new ConcurrentBag<(string Name, Exception Exception)>();
        var executionTasks = new List<Task>();
        var activeTaskCount = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   (!taskQueue.IsEmpty || activeTaskCount > 0))
            {
                if (!taskQueue.IsEmpty)
                {
                    await _throttle.WaitAsync(cancellationToken).ConfigureAwait(false);

                    if (taskQueue.TryDequeue(out var task))
                    {
                        if (HasFailedDependencies(task, failedTasks))
                        {
                            _throttle.Release();
                            _stats.IncrementSkippedTasks();
                            continue;
                        }

                        Interlocked.Increment(ref activeTaskCount);
                        executionTasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                var result = await ExecuteTaskWithMetricsAsync(task, scope, cancellationToken)
                                    .ConfigureAwait(false);

                                if (result is { IsSuccess: false, Error.Exception: not null })
                                {
                                    failedTasks.Add((task.Name, result.Error.Exception));
                                }
                            }
                            finally
                            {
                                _throttle.Release();
                                Interlocked.Decrement(ref activeTaskCount);
                            }
                        }, cancellationToken));
                    }
                    else
                    {
                        _throttle.Release();
                    }
                }
                else
                {
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }
            }

            await Task.WhenAll(executionTasks).ConfigureAwait(false);

            return failedTasks.IsEmpty
                ? Result<Unit, TaskExecutionError>.Success(Unit.Value)
                : CreatePartialSuccessResult(failedTasks);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Task execution failed unexpectedly");
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Task execution failed unexpectedly", null, ex));
        }
    }

    private async Task<Result<Unit, TaskExecutionError>> ExecuteTaskWithMetricsAsync(
        ITask task,
        TaskExecutionScope scope,
        CancellationToken cancellationToken)
    {
        var metrics = _metrics.GetOrAdd(task.Name, _ => new TaskExecutionMetrics());
        var taskMetrics = new ActivityScope($"Task_{task.Name}");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(task.Timeout);

            await task.ExecuteAsync(scope.Context, cts.Token).ConfigureAwait(false);

            taskMetrics.Stop();
            metrics.RecordSuccess(taskMetrics.Elapsed);
            _stats.IncrementCompletedTasks();
            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            taskMetrics.Stop();
            metrics.RecordFailure(taskMetrics.Elapsed);
            _stats.IncrementFailedTasks();
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError($"Task {task.Name} failed", task.Name, ex));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasFailedDependencies(ITask task, ConcurrentBag<(string Name, Exception Exception)> failedTasks)
    {
        return task.Dependencies.Any(dep => failedTasks.Any(f => string.Equals(f.Name, dep, StringComparison.Ordinal)));
    }

    private static Result<Unit, TaskExecutionError> CreatePartialSuccessResult(
        ConcurrentBag<(string Name, Exception Exception)> failedTasks)
    {
        var errors = failedTasks.Select(f => f.Exception).ToList();
        var message = $"Some tasks failed: {string.Join(", ", failedTasks.Select(f => f.Name))}";

        return Result<Unit, TaskExecutionError>.PartialSuccess(
            Unit.Value,
            new TaskExecutionError(message, null, new AggregateException(errors)));
    }
}
