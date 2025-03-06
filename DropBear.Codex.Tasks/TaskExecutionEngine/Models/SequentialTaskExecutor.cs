#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;
using DropBear.Codex.Tasks.TaskExecutionEngine.Extensions;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Sequential task executor for when strict ordering is required.
/// </summary>
public class SequentialTaskExecutor
{
    private readonly ILogger _logger;
    private readonly TaskExecutionTracker _tracker;

    public SequentialTaskExecutor(TaskExecutionTracker tracker)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _logger = LoggerFactory.Logger.ForContext<SequentialTaskExecutor>();
    }

    /// <summary>
    ///     Executes tasks sequentially in the exact order they appear in the queue.
    /// </summary>
    public async Task<Result<Unit, TaskExecutionError>> ExecuteTasksAsync(
        TaskPriorityQueue taskQueue,
        TaskExecutionScope scope,
        CancellationToken cancellationToken)
    {
        var failedTasks = new List<(string Name, Exception Exception)>();
        var context = scope.Context;

        try
        {
            while (!cancellationToken.IsCancellationRequested && !taskQueue.IsEmpty)
            {
                if (!taskQueue.TryDequeue(out var task))
                {
                    continue;
                }

                // Check if this task depends on any failed tasks
                if (HasFailedDependencies(task, failedTasks))
                {
                    _logger.Warning("Skipping task {TaskName} due to failed dependencies", task.Name);
                    continue;
                }

                try
                {
                    // Execute the task and record its result
                    _logger.Information("Executing task {TaskName}", task.Name);
                    _tracker.StartTask(task.Name, Task.CompletedTask);

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(task.Timeout);

                    try
                    {
                        await task.ExecuteAsync(context, cts.Token).ConfigureAwait(false);
                        _tracker.CompleteTask(task.Name, true);
                        context.IncrementCompletedTaskCount();
                    }
                    catch (Exception ex)
                    {
                        _tracker.CompleteTask(task.Name, false);
                        _logger.Error(ex, "Task {TaskName} failed", task.Name);
                        failedTasks.Add((task.Name, ex));

                        // Stop on first failure if configured
                        if (!task.ContinueOnFailure && scope.Context.Options.StopOnFirstFailure)
                        {
                            return Result<Unit, TaskExecutionError>.Failure(
                                new TaskExecutionError($"Task {task.Name} failed and StopOnFirstFailure is enabled",
                                    task.Name, ex));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unexpected error executing task {TaskName}", task.Name);
                    failedTasks.Add((task.Name, ex));
                }
            }

            // Check if execution was cancelled
            if (cancellationToken.IsCancellationRequested)
            {
                return Result<Unit, TaskExecutionError>.Failure(
                    new TaskExecutionError("Task execution was cancelled"));
            }

            // Return appropriate result based on task failures
            if (failedTasks.Count == 0)
            {
                return Result<Unit, TaskExecutionError>.Success(Unit.Value);
            }

            return CreatePartialSuccessResult(failedTasks);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Sequential task execution failed unexpectedly");
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Sequential task execution failed unexpectedly", null, ex));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasFailedDependencies(ITask task, IEnumerable<(string Name, Exception Exception)> failedTasks)
    {
        // If any dependency of 'task' matches a name in 'failedTasks', skip
        return task.Dependencies.Any(dep => failedTasks.Any(f => string.Equals(f.Name, dep, StringComparison.Ordinal)));
    }

    private static Result<Unit, TaskExecutionError> CreatePartialSuccessResult(
        IReadOnlyCollection<(string Name, Exception Exception)> failedTasks)
    {
        var errors = failedTasks.Select(f => f.Exception).ToList();
        var message = $"Some tasks failed: {string.Join(", ", failedTasks.Select(f => f.Name))}";
        return Result<Unit, TaskExecutionError>.PartialSuccess(
            Unit.Value,
            new TaskExecutionError(message, null, new AggregateException(errors)));
    }
}

