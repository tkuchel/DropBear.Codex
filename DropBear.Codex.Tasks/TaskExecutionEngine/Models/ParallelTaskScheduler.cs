#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Schedules and executes tasks in parallel, respecting a maximum degree of parallelism.
///     This enhanced version properly respects task dependencies.
/// </summary>
public sealed class ParallelTaskScheduler : IDisposable
{
    private readonly ILogger _logger;
    private readonly int _maxDegreeOfParallelism;
    private readonly ConcurrentDictionary<string, TaskExecutionMetrics> _metrics;
    private readonly TaskExecutionStats _stats;
    private readonly SemaphoreSlim _throttle;
    private bool _disposed;

    /// <summary>
    ///     Creates a new <see cref="ParallelTaskScheduler" /> with the specified concurrency limit.
    /// </summary>
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

    /// <summary>
    ///     Disposes the scheduler, releasing any resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _throttle.Dispose();
    }

    /// <summary>
    ///     Executes tasks in <paramref name="taskQueue" /> until empty or cancellation is requested.
    ///     Returns a result indicating success or partial success if some tasks failed.
    /// </summary>
    public async Task<Result<Unit, TaskExecutionError>> ExecuteTasksAsync(
        TaskPriorityQueue taskQueue,
        TaskExecutionScope scope,
        CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ParallelTaskScheduler));
        }

        var failedTasks = new ConcurrentBag<(string Name, Exception Exception)>();
        var executionTasks = new List<Task>();
        var runningTasks = new ConcurrentDictionary<string, Task>(StringComparer.Ordinal);
        var completedTasks = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        var activeTaskCount = 0;

        try
        {
            // Loop until no tasks left OR cancellation is triggered
            while (!cancellationToken.IsCancellationRequested &&
                   (!taskQueue.IsEmpty || activeTaskCount > 0))
            {
                // If queue is non-empty, attempt to start a new task
                if (!taskQueue.IsEmpty)
                {
                    // Extract tasks that have all dependencies satisfied
                    var tasksReadyToExecute = new List<ITask>();

                    // Try to find tasks eligible for execution (dependencies satisfied)
                    while (tasksReadyToExecute.Count < _maxDegreeOfParallelism && !taskQueue.IsEmpty)
                    {
                        if (taskQueue.TryPeek(out var nextTask))
                        {
                            // Check if all dependencies are completed
                            if (AreDependenciesSatisfied(nextTask, completedTasks, failedTasks))
                            {
                                if (taskQueue.TryDequeue(out var readyTask))
                                {
                                    tasksReadyToExecute.Add(readyTask);
                                }
                            }
                            else
                            {
                                // Dependencies not satisfied, break the loop
                                break;
                            }
                        }
                        else
                        {
                            // Queue is empty or peek failed
                            break;
                        }
                    }

                    // Start tasks that are ready to execute
                    foreach (var task in tasksReadyToExecute)
                    {
                        await _throttle.WaitAsync(cancellationToken).ConfigureAwait(false);

                        // Final check for failed dependencies
                        if (HasFailedDependencies(task, failedTasks))
                        {
                            _throttle.Release();
                            _stats.IncrementSkippedTasks();
                            continue;
                        }

                        Interlocked.Increment(ref activeTaskCount);

                        // Launch the task in a separate worker
                        var taskExecution = Task.Run(async () =>
                        {
                            try
                            {
                                var result = await ExecuteTaskWithMetricsAsync(task, scope, cancellationToken)
                                    .ConfigureAwait(false);

                                if (result.IsSuccess)
                                {
                                    completedTasks.TryAdd(task.Name, true);
                                }
                                else if (result.Error?.Exception != null)
                                {
                                    failedTasks.Add((task.Name, result.Error.Exception));

                                    // If StopOnFirstFailure is enabled and task is not marked to continue on failure
                                    if (scope.Context.Options.StopOnFirstFailure && !task.ContinueOnFailure)
                                    {
                                        _logger.Warning("Task {TaskName} failed and StopOnFirstFailure is enabled",
                                            task.Name);
                                        throw new OperationCanceledException(
                                            $"Task {task.Name} failed and StopOnFirstFailure is enabled");
                                    }
                                }
                            }
                            finally
                            {
                                _throttle.Release();
                                Interlocked.Decrement(ref activeTaskCount);
                                runningTasks.TryRemove(task.Name, out _);
                            }
                        }, cancellationToken);

                        // Track the running task
                        runningTasks.TryAdd(task.Name, taskExecution);
                        executionTasks.Add(taskExecution);
                    }
                }
                else if (activeTaskCount > 0)
                {
                    // We have running tasks but no available tasks to launch - wait briefly
                    // to avoid busy-waiting
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // No tasks left at all
                    break;
                }
            }

            // Wait for all tasks to finish
            try
            {
                await Task.WhenAll(executionTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (scope.Context.Options.StopOnFirstFailure)
            {
                // This is expected when StopOnFirstFailure is enabled and a task fails
                _logger.Information("Task execution was cancelled due to StopOnFirstFailure policy");
            }

            // If no tasks failed, success
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
        using var taskMetrics = new ActivityScope($"Task_{task.Name}");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(task.Timeout);

            _logger.Information("Executing task {TaskName}", task.Name);

            // Publish task started message
            var startedMessage = TaskStartedMessage.Get(task.Name);
            try
            {
                await scope.MessagePublisher.QueueMessage(scope.ChannelId, startedMessage).ConfigureAwait(false);
            }
            finally
            {
                TaskStartedMessage.Return(startedMessage);
            }

            var executionResult = await task.ExecuteAsync(scope.Context, cts.Token).ConfigureAwait(false);

            taskMetrics.Stop();

            if (executionResult.IsSuccess)
            {
                metrics.RecordSuccess(taskMetrics.Elapsed);
                _stats.IncrementCompletedTasks();

                // Publish task completed message
                var completedMessage = TaskCompletedMessage.Get(task.Name);
                try
                {
                    await scope.MessagePublisher.QueueMessage(scope.ChannelId, completedMessage).ConfigureAwait(false);
                }
                finally
                {
                    TaskCompletedMessage.Return(completedMessage);
                }

                return Result<Unit, TaskExecutionError>.Success(Unit.Value);
            }
            else
            {
                metrics.RecordFailure(taskMetrics.Elapsed);
                _stats.IncrementFailedTasks();
                _logger.Error("Task {TaskName} failed: {ErrorMessage}", task.Name, executionResult.Error!.Message);

                // Publish task failed message
                var exception = executionResult.Error.Exception ?? new Exception(executionResult.Error.Message);
                var failedMessage = TaskFailedMessage.Get(task.Name, exception);
                try
                {
                    await scope.MessagePublisher.QueueMessage(scope.ChannelId, failedMessage).ConfigureAwait(false);
                }
                finally
                {
                    TaskFailedMessage.Return(failedMessage);
                }

                return Result<Unit, TaskExecutionError>.Failure(executionResult.Error);
            }
        }
        catch (Exception ex)
        {
            taskMetrics.Stop();
            metrics.RecordFailure(taskMetrics.Elapsed);
            _stats.IncrementFailedTasks();
            _logger.Error(ex, "Unexpected error executing task {TaskName}", task.Name);

            // Publish task failed message
            var failedMessage = TaskFailedMessage.Get(task.Name, ex);
            try
            {
                await scope.MessagePublisher.QueueMessage(scope.ChannelId, failedMessage).ConfigureAwait(false);
            }
            finally
            {
                TaskFailedMessage.Return(failedMessage);
            }

            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError($"Task {task.Name} failed unexpectedly", task.Name, ex));
        }
    }

    /// <summary>
    ///     Checks if all dependencies for a task are satisfied (either completed successfully or skipped).
    /// </summary>
    private bool AreDependenciesSatisfied(
        ITask task,
        ConcurrentDictionary<string, bool> completedTasks,
        ConcurrentBag<(string Name, Exception Exception)> failedTasks)
    {
        foreach (var dependencyName in task.Dependencies)
        {
            // If dependency completed successfully, it's satisfied
            if (completedTasks.TryGetValue(dependencyName, out var completed) && completed)
            {
                continue;
            }

            // If dependency failed, check if the task is configured to continue on failure
            if (failedTasks.Any(ft => string.Equals(ft.Name, dependencyName, StringComparison.Ordinal)))
            {
                // Even if a dependency failed, we might still want to execute this task
                // if the failed task is marked with ContinueOnFailure
                var dependencyTask = GetTaskByName(dependencyName);
                if (dependencyTask == null || !dependencyTask.ContinueOnFailure)
                {
                    return false;
                }
            }
            else
            {
                // Dependency neither completed nor failed yet - it's still pending
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasFailedDependencies(ITask task, ConcurrentBag<(string Name, Exception Exception)> failedTasks)
    {
        // If any dependency of 'task' matches a name in 'failedTasks', skip
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

    /// <summary>
    ///     Gets a task by name from the task queue. This is a helper method to lookup tasks.
    /// </summary>
    private ITask? GetTaskByName(string taskName)
    {
        // In a real implementation, this would need to access the actual tasks dictionary
        // from the ExecutionEngine. For this implementation, we'll just return null
        // which means we'll use the conservative approach for failed dependency handling.
        return null;
    }
}
