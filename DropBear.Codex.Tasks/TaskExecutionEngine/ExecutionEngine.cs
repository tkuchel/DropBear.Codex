#region

using System.Collections.Concurrent;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IAsyncPublisher<Guid, TaskProgressMessage> _progressPublisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAsyncPublisher<Guid, TaskCompletedMessage> _taskCompletedPublisher;
    private readonly IAsyncPublisher<Guid, TaskFailedMessage> _taskFailedPublisher;

    private readonly List<ITask> _tasks = new();
    private readonly IAsyncPublisher<Guid, TaskStartedMessage> _taskStartedPublisher;
    private DependencyGraph _dependencyGraph = new();

    private TaskExecutionTracker _executionTracker = new();

    // Add a private backing field for IsExecuting
    private volatile bool _isExecuting;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExecutionEngine" /> class.
    /// </summary>
    /// <param name="channelId"> The channel ID for keyed pub/sub </param>
    /// <param name="options">The execution options.</param>
    /// <param name="scopeFactory">The scope factory for the execution engine to use.</param>
    /// <param name="progressPublisher">The publisher for task progress messages.</param>
    /// <param name="taskStartedPublisher">The publisher for task started messages.</param>
    /// <param name="taskCompletedPublisher">The publisher for task completed messages.</param>
    /// <param name="taskFailedPublisher">The publisher for task failed messages.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the required parameters are null.</exception>
    public ExecutionEngine(
        Guid channelId,
        IOptions<ExecutionOptions> options,
        IServiceScopeFactory scopeFactory,
        IAsyncPublisher<Guid, TaskProgressMessage> progressPublisher,
        IAsyncPublisher<Guid, TaskStartedMessage> taskStartedPublisher,
        IAsyncPublisher<Guid, TaskCompletedMessage> taskCompletedPublisher,
        IAsyncPublisher<Guid, TaskFailedMessage> taskFailedPublisher)
    {
        _channelId = channelId;
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _progressPublisher = progressPublisher ?? throw new ArgumentNullException(nameof(progressPublisher));
        _taskStartedPublisher = taskStartedPublisher ?? throw new ArgumentNullException(nameof(taskStartedPublisher));
        _taskCompletedPublisher =
            taskCompletedPublisher ?? throw new ArgumentNullException(nameof(taskCompletedPublisher));
        _taskFailedPublisher = taskFailedPublisher ?? throw new ArgumentNullException(nameof(taskFailedPublisher));
        _logger = LoggerFactory.Logger.ForContext<ExecutionEngine>();
    }

    /// <summary>
    ///     Gets a value indicating whether the execution engine is currently executing tasks.
    /// </summary>
    public bool IsExecuting => _isExecuting;

    /// <summary>
    ///     Clears all tasks from the execution engine.
    /// </summary>
    /// <returns>A Result indicating the success or failure of the operation.</returns>
    public Result<Unit, TaskExecutionError> ClearTasks()
    {
        try
        {
            if (_isExecuting)
            {
                _logger.Warning("Cannot clear tasks while execution is in progress.");
                return Result<Unit, TaskExecutionError>.Failure(
                    new TaskExecutionError("Cannot clear tasks while execution is in progress"));
            }

            if (_tasks.Count == 0)
            {
                _logger.Debug("No tasks to clear from the execution engine.");
                return Result<Unit, TaskExecutionError>.Success(Unit.Value);
            }

            var taskCount = _tasks.Count;
            _tasks.Clear();
            _dependencyGraph = new DependencyGraph(); // Reset dependency graph
            _executionTracker = new TaskExecutionTracker(); // Reset execution tracker

            _logger.Information("Cleared {Count} tasks from the execution engine.", taskCount);
            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to clear tasks from the execution engine.");
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Failed to clear tasks", null, ex));
        }
    }

    /// <summary>
    ///     Reports progress for the current task execution.
    /// </summary>
    /// <param name="taskName">The name of the task reporting progress.</param>
    /// <param name="progress">The progress percentage value between 0 and 100.</param>
    /// <param name="message">Optional message describing the progress state.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="taskName" /> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the execution engine is not currently executing.</exception>
    public async Task ReportProgressAsync(
        string taskName,
        double progress,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }

        if (!_isExecuting)
        {
            throw new InvalidOperationException("Cannot report progress when execution engine is not running.");
        }

        try
        {
            progress = Math.Clamp(progress, 0, 100);

            // Ensure total tasks are properly initialized
            var completedTasks = _executionTracker.GetStats().CompletedTasks;
            var totalTasks = _executionTracker.GetStats().TotalTasks;

            if (totalTasks == 0)
            {
                _logger.Error("Total tasks are not initialized. Unable to report progress.");
                return;
            }

            await _progressPublisher.PublishAsync(
                _channelId,
                new TaskProgressMessage(
                    taskName,
                    progress,
                    completedTasks,
                    totalTasks,
                    TaskStatus.InProgress,
                    message ?? $"Task '{taskName}' progress: {progress:F1}%"),
                cancellationToken
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to report progress for task '{TaskName}'", taskName);
        }
    }


    /// <summary>
    ///     Validates a task before execution.
    /// </summary>
    private async Task<Result<Unit, TaskExecutionError>> ValidateTaskAsync(
        ITask task,
        TaskExecutionScope executionScope)
    {
        try
        {
            // Validate IAsyncValidatable tasks
            if (task is IAsyncValidatable asyncValidatable)
            {
                if (!await asyncValidatable.ValidateAsync().ConfigureAwait(false))
                {
                    _logger.Warning("Async validation failed for task '{TaskName}'.", task.Name);
                    return Result<Unit, TaskExecutionError>.Failure(
                        new TaskExecutionError("Task validation failed", task.Name));
                }
            }

            // Validate ITask tasks
            if (!task.Validate())
            {
                _logger.Warning("Validation failed for task '{TaskName}'.", task.Name);
                return Result<Unit, TaskExecutionError>.Failure(
                    new TaskExecutionError("Task validation failed", task.Name));
            }

            // Check conditions
            if (task.Condition != null && !task.Condition(executionScope.Context))
            {
                _logger.Information("Skipping task '{TaskName}' due to condition.", task.Name);
                return Result<Unit, TaskExecutionError>.PartialSuccess(
                    Unit.Value,
                    new TaskExecutionError("Task skipped due to condition", task.Name));
            }

            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Task validation threw an exception", task.Name, ex));
        }
    }


    /// <summary>
    ///     Executes the compensation action for a failed task.
    /// </summary>
    private async Task<Result<Unit, TaskExecutionError>> ExecuteCompensationAsync(
        ITask task,
        TaskExecutionScope executionScope)
    {
        try
        {
            await task.CompensationActionAsync!(executionScope.Context).ConfigureAwait(false);
            _logger.Information("Compensation action executed for task '{TaskName}'.", task.Name);
            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Compensation action for task '{TaskName}' failed.", task.Name);
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Compensation action failed", task.Name, ex));
        }
    }

    /// <summary>
    ///     Executes a task with retry logic using Polly.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="executionScope">The execution scope containing context and services.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A Result indicating success or failure of the task execution.</returns>
    private async Task<Result<Unit, TaskExecutionError>> ExecuteWithRetryAsync(
        ITask task,
        TaskExecutionScope executionScope,
        CancellationToken cancellationToken)
    {
        try
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    task.MaxRetryCount,
                    attempt => TimeSpan.FromMilliseconds(task.RetryDelay.TotalMilliseconds *
                                                         Math.Pow(2, attempt - 1)), // Exponential backoff
                    (exception, duration, attemptNumber, context) =>
                    {
                        _logger.Warning(
                            exception,
                            "Retry {RetryCount} of {MaxRetries} for task '{TaskName}' after {Delay}s due to error: {ErrorMessage}",
                            attemptNumber,
                            task.MaxRetryCount,
                            task.Name,
                            duration.TotalSeconds,
                            exception.Message);
                    });

            await retryPolicy.ExecuteAsync(
                async ct =>
                {
                    ct.ThrowIfCancellationRequested();
                    await task.ExecuteAsync(executionScope.Context, ct).ConfigureAwait(false);
                },
                cancellationToken
            ).ConfigureAwait(false);

            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError($"Task failed after {task.MaxRetryCount} retry attempts", task.Name, ex));
        }
    }


    /// <summary>
    ///     Updates the overall progress of task execution after a task completes.
    /// </summary>
    /// <param name="task">The task that was just completed.</param>
    /// <param name="context">The execution context containing progress information.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task UpdateProgressAsync(ITask task, ExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            context.IncrementCompletedTaskCount();
            await _progressPublisher.PublishAsync(
                _channelId,
                new TaskProgressMessage(
                    task.Name,
                    null, // TaskProgressPercentage
                    context.CompletedTaskCount, // OverallCompletedTasks
                    context.TotalTaskCount, // OverallTotalTasks
                    TaskStatus.InProgress,
                    $"Task '{task.Name}' execution progress."
                ),
                cancellationToken
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update progress for task '{TaskName}'", task.Name);
            // We don't want to fail the task execution just because progress update failed
        }
    }


    /// <summary>
    ///     Resolves task dependencies and returns a list of tasks in execution order.
    /// </summary>
    private async Task<Result<List<ITask>, TaskExecutionError>> ResolveDependenciesAsync()
    {
        try
        {
            var sortedTasks = new List<ITask>();
            var visited = new Dictionary<string, bool>(StringComparer.Ordinal);

            foreach (var task in _tasks)
            {
                foreach (var dependencyName in task.Dependencies)
                {
                    if (!_tasks.Exists(t => string.Equals(t.Name, dependencyName, StringComparison.Ordinal)))
                    {
                        _logger.Warning(
                            "Task '{TaskName}' has an unresolved dependency: '{DependencyName}'.",
                            task.Name,
                            dependencyName);
                        return Result<List<ITask>, TaskExecutionError>.Failure(
                            new TaskExecutionError($"Unresolved dependency: '{dependencyName}'", task.Name));
                    }
                }

                var visitResult = await VisitAsync(task, visited, sortedTasks).ConfigureAwait(false);
                if (!visitResult.IsSuccess)
                {
                    if (visitResult.Error != null)
                    {
                        return Result<List<ITask>, TaskExecutionError>.Failure(visitResult.Error);
                    }

                    return Result<List<ITask>, TaskExecutionError>.PartialSuccess(
                        sortedTasks,
                        new TaskExecutionError("Failed to resolve task dependencies"));
                }
            }

            _logger.Debug("Task dependencies resolved successfully.");
            return Result<List<ITask>, TaskExecutionError>.Success(sortedTasks);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to resolve task dependencies.");
            return Result<List<ITask>, TaskExecutionError>.Failure(
                new TaskExecutionError("Failed to resolve task dependencies", null, ex));
        }
    }


    private async Task<Result<Unit, TaskExecutionError>> VisitAsync(
        ITask task,
        Dictionary<string, bool> visited,
        List<ITask> sortedTasks)
    {
        if (visited.TryGetValue(task.Name, out var inProcess))
        {
            if (inProcess)
            {
                return Result<Unit, TaskExecutionError>.Failure(
                    new TaskExecutionError("Circular dependency detected", task.Name));
            }

            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }

        visited[task.Name] = true;

        foreach (var dependencyName in task.Dependencies)
        {
            var dependencyTask = _tasks.Find(t =>
                string.Equals(t.Name, dependencyName, StringComparison.Ordinal));

            if (dependencyTask == null)
            {
                return Result<Unit, TaskExecutionError>.Failure(
                    new TaskExecutionError(
                        $"Task '{task.Name}' depends on unknown task '{dependencyName}'",
                        task.Name));
            }

            var visitResult = await VisitAsync(dependencyTask, visited, sortedTasks).ConfigureAwait(false);
            if (!visitResult.IsSuccess)
            {
                return visitResult;
            }
        }

        visited[task.Name] = false;
        if (!sortedTasks.Contains(task))
        {
            sortedTasks.Add(task);
        }

        return Result<Unit, TaskExecutionError>.Success(Unit.Value);
    }

    private async Task<Result<Unit, TaskExecutionError>> ExecuteTasksAsync(
        List<ITask> tasks,
        TaskExecutionScope executionScope,
        CancellationToken cancellationToken)
    {
        var taskStatus = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        var failedTasks = new List<(string TaskName, Exception Exception)>();

        foreach (var task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (task.Dependencies.Any(dep => !taskStatus.GetValueOrDefault(dep)))
            {
                _logger.Warning("Skipping task '{TaskName}' due to failed dependencies.", task.Name);
                taskStatus[task.Name] = false;
                continue;
            }

            try
            {
                var executeResult = await ExecuteTaskAsync(task, executionScope, cancellationToken)
                    .ConfigureAwait(false);

                taskStatus[task.Name] = executeResult.IsSuccess;

                if (!executeResult.IsSuccess)
                {
                    failedTasks.Add((task.Name, executeResult.Error?.Exception!));
                    if (_options.StopOnFirstFailure || !task.ContinueOnFailure)
                    {
                        if (executeResult.Error != null)
                        {
                            return Result<Unit, TaskExecutionError>.Failure(executeResult.Error);
                        }

                        return Result<Unit, TaskExecutionError>.PartialSuccess(
                            Unit.Value,
                            new TaskExecutionError("Some tasks failed"));
                    }
                }
            }
            finally
            {
                // Update to use the context from executionScope
                await UpdateProgressAsync(task, executionScope.Context, cancellationToken).ConfigureAwait(false);
            }
        }

        return failedTasks.Count == 0
            ? Result<Unit, TaskExecutionError>.Success(Unit.Value)
            : Result<Unit, TaskExecutionError>.PartialSuccess(
                Unit.Value,
                new TaskExecutionError(
                    $"Some tasks failed: {string.Join(", ", failedTasks.Select(f => f.TaskName))}"));
    }

    public Result<Unit, TaskExecutionError> AddTask(ITask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        try
        {
            if (_isExecuting)
            {
                _logger.Warning("Cannot add tasks while execution is in progress.");
                return Result<Unit, TaskExecutionError>.Failure(
                    new TaskExecutionError("Cannot add tasks while execution is in progress"));
            }

            if (_tasks.Exists(t => string.Equals(t.Name, task.Name, StringComparison.Ordinal)))
            {
                _logger.Warning("A task with the name '{TaskName}' already exists. Skipping addition.", task.Name);
                return Result<Unit, TaskExecutionError>.PartialSuccess(
                    Unit.Value,
                    new TaskExecutionError($"Task with name '{task.Name}' already exists", task.Name));
            }

            // Validate dependencies to avoid introducing cycles
            foreach (var dependency in task.Dependencies)
            {
                if (!_tasks.Exists(t => string.Equals(t.Name, dependency, StringComparison.Ordinal)))
                {
                    _logger.Warning(
                        "Task '{TaskName}' has an unresolved dependency: '{DependencyName}'.",
                        task.Name,
                        dependency);
                    return Result<Unit, TaskExecutionError>.Failure(
                        new TaskExecutionError($"Unresolved dependency: '{dependency}'", task.Name));
                }
            }

            // Add task and its dependencies to the graph
            _tasks.Add(task);
            foreach (var dependency in task.Dependencies)
            {
                _dependencyGraph.AddDependency(task.Name, dependency);
            }

            _logger.Debug("Added task '{TaskName}' to the execution engine.", task.Name);
            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Failed to add task", task.Name, ex));
        }
    }


    private async Task<Result<Unit, TaskExecutionError>> ExecuteTaskAsync(
        ITask task,
        TaskExecutionScope executionScope,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var validationResult = await ValidateTaskAsync(task, executionScope).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            executionScope.Tracker.StartTask(task.Name);

            await _taskStartedPublisher
                .PublishAsync(_channelId, new TaskStartedMessage(task.Name), cancellationToken)
                .ConfigureAwait(false);

            _logger.Debug("Starting task '{TaskName}'.", task.Name);

            var executionResult = await ExecuteWithRetryAsync(task, executionScope, cancellationToken)
                .ConfigureAwait(false);

            executionScope.Tracker.CompleteTask(task.Name, executionResult.IsSuccess);

            if (executionResult.IsSuccess)
            {
                await _taskCompletedPublisher
                    .PublishAsync(_channelId, new TaskCompletedMessage(task.Name), cancellationToken)
                    .ConfigureAwait(false);

                _logger.Debug("Task '{TaskName}' completed successfully.", task.Name);
                return Result<Unit, TaskExecutionError>.Success(Unit.Value);
            }

            // Handle failure
            if (executionResult.Error?.Exception != null)
            {
                return await HandleTaskFailureAsync(task, executionScope, executionResult.Error, cancellationToken)
                    .ConfigureAwait(false);
            }

            return executionResult;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            executionScope.Tracker.CompleteTask(task.Name, false);
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Unexpected error executing task", task.Name, ex));
        }
    }

    private async Task<Result<Unit, TaskExecutionError>> HandleTaskFailureAsync(
        ITask task,
        TaskExecutionScope executionScope,
        TaskExecutionError error,
        CancellationToken cancellationToken)
    {
        await _taskFailedPublisher
            .PublishAsync(_channelId, new TaskFailedMessage(task.Name, error.Exception!), cancellationToken)
            .ConfigureAwait(false);

        _logger.Error(error.Exception, "Task '{TaskName}' failed.", task.Name);

        if (task.CompensationActionAsync == null)
        {
            return Result<Unit, TaskExecutionError>.Failure(error);
        }

        var compensationResult = await ExecuteCompensationAsync(task, executionScope).ConfigureAwait(false);
        if (!compensationResult.IsSuccess)
        {
            return Result<Unit, TaskExecutionError>.Failure(new TaskExecutionError(
                "Task failed and compensation action also failed",
                task.Name,
                new AggregateException(error.Exception!, compensationResult.Error!.Exception!)
            ));
        }

        return Result<Unit, TaskExecutionError>.Failure(error);
    }

    public async Task<Result<Unit, TaskExecutionError>> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!PreExecutionChecks(out var preExecutionError))
        {
            return Result<Unit, TaskExecutionError>.Failure(preExecutionError);
        }

        using var rootScope = _scopeFactory.CreateScope();
        var executionScope = CreateExecutionScope(rootScope);

        try
        {
            var tasks = await ResolveDependenciesAsync().ConfigureAwait(false);
            if (!tasks.IsSuccess)
            {
                return Result<Unit, TaskExecutionError>.Failure(tasks.Error);
            }

            return await ExecuteResolvedTasksAsync(tasks.Value!, executionScope, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            await FinalizeExecutionAsync(executionScope).ConfigureAwait(false);
        }
    }

    private bool PreExecutionChecks(out TaskExecutionError? error)
    {
        error = null;

        if (_isExecuting)
        {
            _logger.Warning("Execution is already in progress.");
            error = new TaskExecutionError("Execution is already in progress");
            return false;
        }

        if (_tasks.Count == 0)
        {
            _logger.Warning("No tasks to execute. Task list is empty.");
            error = new TaskExecutionError("No tasks to execute");
            return false;
        }

        if (_dependencyGraph.HasCycle())
        {
            _logger.Error("Circular dependency detected in the task graph.");
            error = new TaskExecutionError("Circular dependency detected in task graph");
            return false;
        }

        // Set total task count
        _executionTracker.SetTotalTaskCount(_tasks.Count);

        _isExecuting = true;
        _logger.Information("Pre-execution checks passed. Starting task execution.");
        return true;
    }


    private TaskExecutionScope CreateExecutionScope(IServiceScope rootScope)
    {
        return new TaskExecutionScope(
            rootScope,
            _executionTracker,
            new ExecutionContext(_logger, _options, _scopeFactory) { TotalTaskCount = _tasks.Count },
            _logger);
    }

    private async Task<Result<Unit, TaskExecutionError>> ExecuteResolvedTasksAsync(
        List<ITask> tasks,
        TaskExecutionScope executionScope,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteTasksAsync(tasks, executionScope, cancellationToken).ConfigureAwait(false);
        LogExecutionStats(executionScope.Tracker.GetStats());
        return result;
    }

    private async Task FinalizeExecutionAsync(TaskExecutionScope executionScope)
    {
        _isExecuting = false;
        _logger.Information("Task execution completed.");

        try
        {
            await executionScope.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed during execution scope disposal.");
        }
    }


    private void LogExecutionStats(TaskExecutionStats stats)
    {
        _logger.Information(
            "Task Execution Summary: Total={TotalTasks}, Completed={CompletedTasks}, Failed={FailedTasks}, Skipped={SkippedTasks}",
            stats.TotalTasks,
            stats.CompletedTasks,
            stats.FailedTasks,
            stats.SkippedTasks);

        foreach (var duration in stats.TaskDurations)
        {
            _logger.Debug(
                "Task '{TaskName}' completed in {Duration:g}.",
                duration.Key,
                duration.Value);
        }
    }
}
