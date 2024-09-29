#region

using System.Collections.Concurrent;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using MessagePipe;
using Microsoft.Extensions.Options;
using Polly;
using Serilog;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;
using TaskStatus = DropBear.Codex.Tasks.TaskExecutionEngine.Enums.TaskStatus;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Represents the execution engine that manages and executes tasks with dependency resolution and retry logic.
/// </summary>
public sealed class ExecutionEngine
{
    private readonly Guid _channelId;
    private readonly ILogger _logger;
    private readonly ExecutionOptions _options;
    private readonly IPublisher<Guid, TaskProgressMessage> _progressPublisher;
    private readonly IPublisher<Guid, TaskCompletedMessage> _taskCompletedPublisher;
    private readonly IPublisher<Guid, TaskFailedMessage> _taskFailedPublisher;
    private readonly List<ITask> _tasks = new();
    private readonly IPublisher<Guid, TaskStartedMessage> _taskStartedPublisher;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExecutionEngine" /> class.
    /// </summary>
    /// <param name="channelId"> The channel Id for keyed pub/sub </param>
    /// <param name="options">The execution options.</param>
    /// <param name="progressPublisher">The publisher for task progress messages.</param>
    /// <param name="taskStartedPublisher">The publisher for task started messages.</param>
    /// <param name="taskCompletedPublisher">The publisher for task completed messages.</param>
    /// <param name="taskFailedPublisher">The publisher for task failed messages.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the required parameters are null.</exception>
    public ExecutionEngine(
        Guid channelId,
        IOptions<ExecutionOptions> options,
        IPublisher<Guid, TaskProgressMessage> progressPublisher,
        IPublisher<Guid, TaskStartedMessage> taskStartedPublisher,
        IPublisher<Guid, TaskCompletedMessage> taskCompletedPublisher,
        IPublisher<Guid, TaskFailedMessage> taskFailedPublisher)
    {
        _channelId = channelId;
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _progressPublisher = progressPublisher ?? throw new ArgumentNullException(nameof(progressPublisher));
        _taskStartedPublisher = taskStartedPublisher ?? throw new ArgumentNullException(nameof(taskStartedPublisher));
        _taskCompletedPublisher =
            taskCompletedPublisher ?? throw new ArgumentNullException(nameof(taskCompletedPublisher));
        _taskFailedPublisher = taskFailedPublisher ?? throw new ArgumentNullException(nameof(taskFailedPublisher));
        _logger = LoggerFactory.Logger.ForContext<ExecutionEngine>();
    }

    /// <summary>
    ///     Adds a task to the execution engine.
    /// </summary>
    /// <param name="task">The task to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="task" /> is null.</exception>
    public Result AddTask(ITask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        try
        {
            if (_tasks.Exists(t => string.Equals(t.Name, task.Name, StringComparison.Ordinal)))
            {
                _logger.Warning("A task with the name '{TaskName}' already exists. Skipping addition.", task.Name);
                return Result.PartialSuccess("Task with the name '{TaskName}' already exists.");
            }

            _tasks.Add(task);
            _logger.Information("Added task '{TaskName}' to the execution engine.", task.Name);
            return Result.Success();
        }
        catch (Exception e)
        {
            return Result.Failure(e.Message, e);
        }
    }

    /// <summary>
    ///     Executes the tasks managed by the execution engine asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when task execution fails and cannot continue.</exception>
    public async Task<Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (_tasks.Count == 0)
        {
            _logger.Warning("No tasks to execute.");
            return Result.PartialSuccess("No tasks to execute.");
        }

        var executionContext = new ExecutionContext(_logger, _options) { TotalTaskCount = _tasks.Count };

        List<ITask> sortedTasks;
        try
        {
            sortedTasks = ResolveDependencies();
            _logger.Information("Task dependencies resolved successfully.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to resolve task dependencies.");
            return Result.Failure("Failed to resolve task dependencies.", ex);
        }

        try
        {
            await ExecuteTasksAsync(sortedTasks, executionContext, cancellationToken).ConfigureAwait(false);
            _logger.Information("All tasks executed successfully.");
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Task execution was canceled.");
            return Result.Warning("Task execution was canceled.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred during task execution.");
            return Result.Failure("An error occurred during task execution.", ex);
        }
    }

    /// <summary>
    ///     Resolves task dependencies and returns a list of tasks in execution order.
    /// </summary>
    /// <returns>A list of tasks sorted based on dependencies.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a circular dependency is detected or dependencies are missing.</exception>
    private List<ITask> ResolveDependencies()
    {
        var sortedTasks = new List<ITask>();
        var visited = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var task in _tasks)
        {
            Visit(task, visited, sortedTasks);
        }

        sortedTasks.Reverse();
        return sortedTasks;
    }

    /// <summary>
    ///     Visits a task and its dependencies recursively for topological sorting.
    /// </summary>
    /// <param name="task">The task to visit.</param>
    /// <param name="visited">A dictionary tracking visited tasks.</param>
    /// <param name="sortedTasks">The list of sorted tasks.</param>
    /// <exception cref="InvalidOperationException">Thrown when a circular dependency is detected or dependencies are missing.</exception>
    private void Visit(ITask task, Dictionary<string, bool> visited, List<ITask> sortedTasks)
    {
        if (visited.TryGetValue(task.Name, out var inProcess))
        {
            if (inProcess)
            {
                throw new InvalidOperationException($"Circular dependency detected on task '{task.Name}'.");
            }

            return;
        }

        visited[task.Name] = true;

        foreach (var dependencyName in task.Dependencies)
        {
            var dependencyTask =
                _tasks.Find(t => string.Equals(t.Name, dependencyName, StringComparison.Ordinal));
            if (dependencyTask == null)
            {
                throw new InvalidOperationException($"Task '{task.Name}' depends on unknown task '{dependencyName}'.");
            }

            Visit(dependencyTask, visited, sortedTasks);
        }

        visited[task.Name] = false;
        if (!sortedTasks.Contains(task))
        {
            sortedTasks.Add(task);
        }
    }

    /// <summary>
    ///     Executes the sorted tasks asynchronously.
    /// </summary>
    /// <param name="tasks">The sorted list of tasks to execute.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ExecuteTasksAsync(List<ITask> tasks, ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var taskStatus = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        foreach (var task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if dependencies succeeded
            if (task.Dependencies.Any(dep => !taskStatus.GetValueOrDefault(dep)))
            {
                _logger.Warning("Skipping task '{TaskName}' due to failed dependencies.", task.Name);
                taskStatus[task.Name] = false;
                continue;
            }

            // Execute task
            try
            {
                await ExecuteTaskAsync(task, context, cancellationToken).ConfigureAwait(false);
                taskStatus[task.Name] = true;
                _logger.Information("Task '{TaskName}' executed successfully.", task.Name);
            }
            catch (Exception ex)
            {
                taskStatus[task.Name] = false;
                _logger.Error(ex, "Task '{TaskName}' failed.", task.Name);

                if (_options.StopOnFirstFailure || !task.ContinueOnFailure)
                {
                    throw new InvalidOperationException($"Task '{task.Name}' failed and execution cannot continue.",
                        ex);
                }
            }
            finally
            {
                context.IncrementCompletedTaskCount();
                _progressPublisher.Publish(_channelId, new TaskProgressMessage(
                    task.Name,
                    context.CompletedTaskCount,
                    context.TotalTaskCount,
                    TaskStatus.InProgress,
                    $"Task '{task.Name}' execution progress."
                ));
            }
        }
    }

    /// <summary>
    ///     Executes a single task with retry logic.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ExecuteTaskAsync(ITask task, ExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Check condition
        if (task.Condition != null && !task.Condition(context))
        {
            _logger.Information("Skipping task '{TaskName}' due to condition.", task.Name);
            return;
        }

        // Validate task
        if (!task.Validate())
        {
            _logger.Warning("Validation failed for task '{TaskName}'.", task.Name);
            throw new InvalidOperationException($"Validation failed for task '{task.Name}'.");
        }

        _taskStartedPublisher.Publish(_channelId, new TaskStartedMessage(task.Name));
        _logger.Information("Starting task '{TaskName}'.", task.Name);

        try
        {
            await ExecuteWithRetryAsync(task, context, cancellationToken).ConfigureAwait(false);
            _taskCompletedPublisher.Publish(_channelId, new TaskCompletedMessage(task.Name));
            _logger.Information("Task '{TaskName}' completed successfully.", task.Name);
        }
        catch (Exception ex)
        {
            _taskFailedPublisher.Publish(_channelId, new TaskFailedMessage(task.Name, ex));
            _logger.Error(ex, "Task '{TaskName}' failed.", task.Name);

            if (task.CompensationActionAsync != null)
            {
                try
                {
                    await task.CompensationActionAsync(context).ConfigureAwait(false);
                    _logger.Information("Compensation action executed for task '{TaskName}'.", task.Name);
                }
                catch (Exception compEx)
                {
                    _logger.Error(compEx, "Compensation action for task '{TaskName}' failed.", task.Name);
                }
            }

            throw; // Re-throw to be handled by the caller
        }
    }

    /// <summary>
    ///     Executes a task with retry logic using Polly.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ExecuteWithRetryAsync(ITask task, ExecutionContext context, CancellationToken cancellationToken)
    {
        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                task.MaxRetryCount,
                retryAttempt => task.RetryDelay,
                (exception, timeSpan, retryCount, ctx) =>
                {
                    _logger.Warning(
                        exception,
                        "Retry {RetryCount} for task '{TaskName}' after {Delay}s due to error: {ErrorMessage}",
                        retryCount,
                        task.Name,
                        timeSpan.TotalSeconds,
                        exception.Message);
                }
            );

        await retryPolicy.ExecuteAsync(async ct =>
        {
            ct.ThrowIfCancellationRequested();
            await task.ExecuteAsync(context, ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }
}
