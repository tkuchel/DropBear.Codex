#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;
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
public sealed class ExecutionEngine : IAsyncDisposable, IDisposable
{
    private static readonly ObjectPool<TaskProgressMessage> ProgressMessagePool =
        new DefaultObjectPoolProvider().Create(new DefaultPooledObjectPolicy<TaskProgressMessage>());

    private readonly Guid _channelId;
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly object _disposalLock = new();

    private readonly ILogger _logger;

    private readonly ExecutionOptions _options;
    private readonly IAsyncPublisher<Guid, TaskProgressMessage> _progressPublisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAsyncPublisher<Guid, TaskCompletedMessage> _taskCompletedPublisher;
    private readonly IAsyncPublisher<Guid, TaskFailedMessage> _taskFailedPublisher;

    private readonly ConcurrentDictionary<string, ITask> _tasks = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _tasksLock = new(1, 1); // Lock for task management

    private readonly IAsyncPublisher<Guid, TaskStartedMessage> _taskStartedPublisher;
    private DependencyGraph _dependencyGraph = new();
    private bool _disposed;

    private int _isExecuting; // Backing field for thread-safe boolean

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExecutionEngine" /> class.
    /// </summary>
    /// <param name="channelId">The channel ID for keyed pub/sub.</param>
    /// <param name="options">The execution options.</param>
    /// <param name="scopeFactory">The scope factory for creating DI scopes.</param>
    /// <param name="progressPublisher">The publisher for task progress messages.</param>
    /// <param name="taskStartedPublisher">The publisher for task started messages.</param>
    /// <param name="taskCompletedPublisher">The publisher for task completed messages.</param>
    /// <param name="taskFailedPublisher">The publisher for task failed messages.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public ExecutionEngine(
        Guid channelId,
        IOptions<ExecutionOptions> options,
        IServiceScopeFactory scopeFactory,
        IAsyncPublisher<Guid, TaskProgressMessage> progressPublisher,
        IAsyncPublisher<Guid, TaskStartedMessage> taskStartedPublisher,
        IAsyncPublisher<Guid, TaskCompletedMessage> taskCompletedPublisher,
        IAsyncPublisher<Guid, TaskFailedMessage> taskFailedPublisher)
    {
        // Validate parameters
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
    ///     Indicates whether the execution engine is currently executing tasks.
    ///     Thread-safe using atomic operations.
    /// </summary>
    private bool SafeIsExecuting
    {
        get => Interlocked.CompareExchange(ref _isExecuting, 0, 0) == 1;
        set => Interlocked.Exchange(ref _isExecuting, value ? 1 : 0);
    }


    /// <summary>
    ///     Gets a value indicating whether the execution engine is currently executing tasks.
    ///     This property is thread-safe.
    /// </summary>
    public bool IsExecuting => Interlocked.CompareExchange(ref _isExecuting, 0, 0) == 1;

    /// <summary>
    ///     Gets the task execution tracker for monitoring task progress and statistics.
    /// </summary>
    public TaskExecutionTracker Tracker { get; } = new();


    /// <summary>
    ///     Gets the dictionary of tasks being managed by the execution engine.
    ///     This property is read-only.
    /// </summary>
    public IReadOnlyDictionary<string, ITask> Tasks => _tasks;


    /// <summary>
    ///     Indicates whether the execution engine should execute tasks in parallel.
    ///     Default is false to preserve existing behavior.
    /// </summary>
    public bool EnableParallelExecution { get; set; } = false;

    /// <summary>
    ///     Asynchronously disposes of the execution engine and its resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        lock (_disposalLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        try
        {
            if (IsExecuting)
            {
                _logger.Information("Cancelling ongoing tasks during disposal...");
                await _disposalCts.CancelAsync().ConfigureAwait(false);
            }

            await Tracker.WaitForAllTasksToCompleteAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            _tasks.Clear();
            _dependencyGraph = new DependencyGraph();
            Tracker.Reset();
            _tasksLock.Dispose();
            _disposalCts.Dispose();
            _logger.Information("ExecutionEngine disposed successfully.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during ExecutionEngine disposal.");
        }
    }


    /// <summary>
    ///     Synchronously disposes of the execution engine and its resources.
    /// </summary>
    public void Dispose()
    {
        // Block until async disposal completes
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }


    /// <summary>
    ///     Clears all tasks from the execution engine.
    /// </summary>
    /// <returns>A result indicating the success or failure of the operation.</returns>
    public Result<Unit, TaskExecutionError> ClearTasks()
    {
        // Log the calling method
        _logger.Debug("ClearTasks called by {Caller}.", GetCallerName());

        if (SafeIsExecuting)
        {
            _logger.Warning("Cannot clear tasks while execution is in progress.");
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Cannot clear tasks while execution is in progress"));
        }

        if (!_tasks.Any())
        {
            _logger.Debug("No tasks to clear from the execution engine.");
            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }

        // Reset internal state
        ResetEngineState();

        _logger.Information("Cleared all tasks from the execution engine.");
        return Result<Unit, TaskExecutionError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Resets the internal state of the execution engine.
    /// </summary>
    private void ResetEngineState()
    {
        _tasks.Clear();
        _dependencyGraph = new DependencyGraph();
        Tracker.Reset();
    }

    // Add this method to check disposal state
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ExecutionEngine));
        }
    }


    /// <summary>
    ///     Reports progress for a task currently being executed.
    /// </summary>
    /// <param name="taskName">The name of the task.</param>
    /// <param name="progress">Progress percentage (0-100).</param>
    /// <param name="message">Optional message describing the progress state.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task ReportProgressAsync(
        string taskName,
        double progress,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ValidateTaskName(taskName);

        if (!SafeIsExecuting)
        {
            _logger.Warning("Cannot report progress when the execution engine is not running.");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            progress = Math.Clamp(progress, 0, 100);
            var stats = Tracker.GetStats();

            if (stats.TotalTasks == 0)
            {
                _logger.Error("Total tasks are not initialized. Unable to report progress.");
                return;
            }

            // Smooth Progress Simulation for significant jumps
            var lastProgress = Tracker.GetLastProgress(taskName); // A method to track last progress
            if (progress > lastProgress + 5.0 && progress - lastProgress < 50.0) // Adjust thresholds as needed
            {
                await SimulateSmoothProgress(
                    _progressPublisher,
                    _channelId,
                    taskName,
                    lastProgress,
                    progress,
                    500, // Total duration for simulation (ms)
                    10, // Steps for simulation
                    cancellationToken
                ).ConfigureAwait(false);
            }

            // Prepare progress message
            var progressMessage = ProgressMessagePool.Get();
            progressMessage.Initialize(
                taskName,
                progress,
                stats.CompletedTasks,
                stats.TotalTasks,
                TaskStatus.InProgress,
                message ?? $"Task '{taskName}' execution progress.");

            // Publish progress
            await _progressPublisher.PublishAsync(_channelId, progressMessage, cancellationToken).ConfigureAwait(false);

            // Return message to the pool
            ProgressMessagePool.Return(progressMessage);

            // Update last progress
            Tracker.UpdateLastProgress(taskName, progress); // Ensure last progress is stored
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Progress reporting canceled for task '{TaskName}'.", taskName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to report progress for task '{TaskName}'", taskName);
        }
    }


    /// <summary>
    ///     Validates that the provided task name is not null or empty.
    /// </summary>
    private void ValidateTaskName(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }
    }

    /// <summary>
    ///     Validates a task before execution, including conditions and custom validations.
    /// </summary>
    /// <param name="task">The task to validate.</param>
    /// <param name="executionScope">The execution scope.</param>
    /// <returns>A result indicating success or failure of validation.</returns>
    private async Task<Result<Unit, TaskExecutionError>> ValidateTaskAsync(
        ITask task,
        TaskExecutionScope executionScope)
    {
        if (task is IAsyncValidatable asyncValidatable && !await asyncValidatable.ValidateAsync().ConfigureAwait(false))
        {
            return LogValidationFailure(task.Name, "Async validation failed");
        }

        if (!task.Validate())
        {
            return LogValidationFailure(task.Name, "Validation failed");
        }

        if (task.Condition != null && !task.Condition(executionScope.Context))
        {
            _logger.Information("Skipping task '{TaskName}' due to condition.", task.Name);
            return Result<Unit, TaskExecutionError>.PartialSuccess(
                Unit.Value,
                new TaskExecutionError("Task skipped due to condition", task.Name));
        }

        return Result<Unit, TaskExecutionError>.Success(Unit.Value);
    }


    /// <summary>
    ///     Logs a validation failure and returns a failure result.
    /// </summary>
    private Result<Unit, TaskExecutionError> LogValidationFailure(string taskName, string reason)
    {
        _logger.Warning("{Reason} for task '{TaskName}'.", reason, taskName);
        return Result<Unit, TaskExecutionError>.Failure(
            new TaskExecutionError(reason, taskName));
    }


    /// <summary>
    ///     Executes the compensation action for a failed task.
    /// </summary>
    private async Task<Result<Unit, TaskExecutionError>> ExecuteCompensationAsync(
        ITask task,
        TaskExecutionScope executionScope,
        CancellationToken cancellationToken)
    {
        try
        {
            await task.CompensationActionAsync!(executionScope.Context, cancellationToken).ConfigureAwait(false);
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
    ///     Executes a task with retry logic using Polly, including jitter for real-world scenarios.
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
                    attempt =>
                    {
                        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));
                        return TimeSpan.FromMilliseconds(task.RetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)) +
                               jitter;
                    },
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


    private async Task SimulateSmoothProgress(
        IAsyncPublisher<Guid, TaskProgressMessage> progressPublisher,
        Guid channelId,
        string taskName,
        double startProgress,
        double endProgress,
        int totalDurationMs,
        int totalSteps,
        CancellationToken cancellationToken)
    {
        if (startProgress >= endProgress)
        {
            return;
        }

        var delayPerStep = totalDurationMs / totalSteps;
        var progressIncrement = (endProgress - startProgress) / totalSteps;

        for (var i = 0; i < totalSteps; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Debug("Progress simulation canceled for task: {TaskName}", taskName);
                break;
            }

            // Simulated incremental progress
            var currentProgress = startProgress + (i * progressIncrement);
            var progressMessage = ProgressMessagePool.Get();
            progressMessage.Initialize(taskName, currentProgress, null, null, TaskStatus.InProgress,
                "Simulating progress");

            await progressPublisher.PublishAsync(channelId, progressMessage, cancellationToken).ConfigureAwait(false);
            ProgressMessagePool.Return(progressMessage);

            await Task.Delay(delayPerStep, cancellationToken).ConfigureAwait(false);
        }

        // Ensure the final progress value is sent
        var finalProgressMessage = ProgressMessagePool.Get();
        finalProgressMessage.Initialize(taskName, endProgress, null, null, TaskStatus.InProgress,
            "Final simulated progress");

        await progressPublisher.PublishAsync(channelId, finalProgressMessage, cancellationToken).ConfigureAwait(false);
        ProgressMessagePool.Return(finalProgressMessage);
    }


    /// <summary>
    ///     Updates the overall progress of task execution after a task completes.
    /// </summary>
    /// <param name="task">The task that was just completed.</param>
    /// <param name="context">The execution context containing progress information.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateProgressAsync(ITask task, ExecutionContext context, CancellationToken cancellationToken)
    {
        if (_disposed || cancellationToken.IsCancellationRequested)
        {
            _logger.Debug("Skipping progress update for {TaskName}: Engine disposed or cancellation requested.",
                task.Name);
            return;
        }

        try
        {
            _logger.Debug("Attempting progress update for {TaskName}. Caller: {Caller}", task.Name, GetCallerName());

            context.IncrementCompletedTaskCount();
            await _progressPublisher.PublishAsync(
                _channelId,
                new TaskProgressMessage(
                    task.Name,
                    null, // TaskProgressPercentage
                    context.CompletedTaskCount,
                    context.TotalTaskCount,
                    TaskStatus.InProgress,
                    $"Task '{task.Name}' execution progress."
                ),
                cancellationToken
            ).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Progress update canceled for task '{TaskName}'", task.Name);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update progress for task '{TaskName}'", task.Name);
        }
    }

    private static string GetCallerName([CallerMemberName] string callerName = "")
    {
        return callerName;
    }


    /// <summary>
    ///     Resolves task dependencies and returns a list of tasks in execution order.
    /// </summary>
    /// <returns>A result containing the sorted list of tasks or an error.</returns>
    private async Task<Result<List<ITask>, TaskExecutionError>> ResolveDependenciesAsync()
    {
        try
        {
            var sortedTasks = new List<ITask>();
            var visited = new Dictionary<string, TaskVisitState>(StringComparer.Ordinal);

            foreach (var taskPair in _tasks)
            {
                var task = taskPair.Value;

                foreach (var dependencyName in task.Dependencies)
                {
                    if (!_tasks.ContainsKey(dependencyName))
                    {
                        _logger.Warning("Task '{TaskName}' has an unresolved dependency: '{DependencyName}'.",
                            task.Name, dependencyName);
                        return Result<List<ITask>, TaskExecutionError>.Failure(
                            new TaskExecutionError($"Unresolved dependency: '{dependencyName}'", task.Name));
                    }
                }

                var visitResult = await VisitAsync(task, visited, sortedTasks).ConfigureAwait(false);
                if (!visitResult.IsSuccess)
                {
                    return Result<List<ITask>, TaskExecutionError>.Failure(visitResult.Error);
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


    /// <summary>
    ///     Visits a task during dependency resolution to ensure no cycles exist and resolves dependencies.
    /// </summary>
    /// <param name="task">The task being visited.</param>
    /// <param name="visited">A dictionary to track visitation status of tasks.</param>
    /// <param name="sortedTasks">The sorted list of tasks for execution.</param>
    /// <returns>A result indicating success or failure.</returns>
    private async Task<Result<Unit, TaskExecutionError>> VisitAsync(
        ITask task,
        Dictionary<string, TaskVisitState> visited,
        List<ITask> sortedTasks)
    {
        if (visited.TryGetValue(task.Name, out var state))
        {
            if (state == TaskVisitState.Visiting)
            {
                return LogDependencyResolutionError((string)$"Circular dependency detected for task '{task.Name}'.",
                    task.Name);
            }

            if (state == TaskVisitState.Visited)
            {
                return Result<Unit, TaskExecutionError>.Success(Unit.Value);
            }
        }

        visited[task.Name] = TaskVisitState.Visiting;

        foreach (var dependencyName in task.Dependencies)
        {
            if (!_tasks.TryGetValue(dependencyName, out var dependencyTask))
            {
                return LogDependencyResolutionError(
                    (string)$"Task '{task.Name}' depends on an unknown task '{dependencyName}'.", task.Name);
            }

            var visitResult = await VisitAsync(dependencyTask, visited, sortedTasks).ConfigureAwait(false);
            if (!visitResult.IsSuccess)
            {
                return visitResult;
            }
        }

        visited[task.Name] = TaskVisitState.Visited;

        if (!sortedTasks.Contains(task))
        {
            sortedTasks.Add(task);
        }

        return Result<Unit, TaskExecutionError>.Success(Unit.Value);
    }


    /// <summary>
    ///     Logs and returns a dependency resolution error.
    /// </summary>
    private Result<Unit, TaskExecutionError> LogDependencyResolutionError(string message, string taskName)
    {
        _logger.Error(message);
        return Result<Unit, TaskExecutionError>.Failure(new TaskExecutionError(message, taskName));
    }


    /// <summary>
    ///     Executes tasks with support for sequential or parallel execution, including smooth progress reporting.
    /// </summary>
    /// <param name="tasks">The list of tasks to execute.</param>
    /// <param name="executionScope">The execution scope containing context and services.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A result indicating success or failure.</returns>
    private async Task<Result<Unit, TaskExecutionError>> ExecuteTasksAsync(
        List<ITask> tasks,
        TaskExecutionScope executionScope,
        CancellationToken cancellationToken)
    {
        if (EnableParallelExecution)
        {
            var taskStatus = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

            var taskResults = await Task.WhenAll(tasks.Select(async task =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Check dependencies
                    var dependenciesFailed = task.Dependencies.Any(dep => !taskStatus.GetValueOrDefault(dep));
                    if (dependenciesFailed)
                    {
                        _logger.Warning("Skipping task '{TaskName}' due to failed dependencies.", task.Name);
                        taskStatus[task.Name] = false;
                        return Result<Unit, TaskExecutionError>.Failure(
                            new TaskExecutionError("Dependency failed", task.Name));
                    }

                    // Simulate smooth progress before execution
                    await SimulateSmoothProgress(
                        _progressPublisher,
                        _channelId,
                        task.Name,
                        0,
                        50, // Progress to simulate before execution
                        500, // Duration for simulation (ms)
                        10, // Steps for simulation
                        cancellationToken
                    ).ConfigureAwait(false);

                    // Execute the task
                    var executeResult = await ExecuteTaskAsync(task, executionScope, cancellationToken)
                        .ConfigureAwait(false);
                    taskStatus[task.Name] = executeResult.IsSuccess;

                    // Report final progress
                    if (executeResult.IsSuccess)
                    {
                        var progressMessage = ProgressMessagePool.Get();
                        progressMessage.Initialize(task.Name, 100.0, null, null, TaskStatus.Completed,
                            "Task completed");
                        await _progressPublisher.PublishAsync(_channelId, progressMessage, cancellationToken)
                            .ConfigureAwait(false);
                        ProgressMessagePool.Return(progressMessage);
                    }

                    return executeResult;
                }
                catch (OperationCanceledException)
                {
                    _logger.Warning("Execution canceled for task '{TaskName}'.", task.Name);
                    return Result<Unit, TaskExecutionError>.Failure(
                        new TaskExecutionError("Task execution canceled", task.Name));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error executing task '{TaskName}'.", task.Name);
                    return Result<Unit, TaskExecutionError>.Failure(
                        new TaskExecutionError("Unexpected task error", task.Name, ex));
                }
            })).ConfigureAwait(false);

            // Aggregate results
            var failedResults = taskResults.Where(r => !r.IsSuccess).ToList();
            return failedResults.Any()
                ? Result<Unit, TaskExecutionError>.PartialSuccess(Unit.Value,
                    new TaskExecutionError("Some tasks failed"))
                : Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }

        // Sequential execution
        return await ExecuteTasksSequentiallyAsync(tasks, executionScope, cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    ///     Executes tasks sequentially, respecting dependency order.
    /// </summary>
    /// <param name="tasks">The list of tasks to execute.</param>
    /// <param name="executionScope">The execution scope containing context and services.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A result indicating success or failure.</returns>
    private async Task<Result<Unit, TaskExecutionError>> ExecuteTasksSequentiallyAsync(
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
                var executeResult =
                    await ExecuteTaskAsync(task, executionScope, cancellationToken).ConfigureAwait(false);
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


    /// <summary>
    ///     Adds a task to the execution engine after validation.
    /// </summary>
    /// <param name="task">The task to add.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result<Unit, TaskExecutionError> AddTask(ITask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        ThrowIfDisposed();

        try
        {
            if (SafeIsExecuting)
            {
                _logger.Warning("Cannot add tasks while execution is in progress.");
                return Result<Unit, TaskExecutionError>.Failure(
                    new TaskExecutionError("Cannot add tasks while execution is in progress"));
            }

            if (!_tasks.TryAdd(task.Name, task))
            {
                _logger.Warning("A task with the name '{TaskName}' already exists. Skipping addition.", task.Name);
                return Result<Unit, TaskExecutionError>.PartialSuccess(
                    Unit.Value,
                    new TaskExecutionError($"Task with name '{task.Name}' already exists", task.Name));
            }

            foreach (var dependency in task.Dependencies)
            {
                if (!_tasks.ContainsKey(dependency))
                {
                    _logger.Warning(
                        "Task '{TaskName}' has an unresolved dependency: '{DependencyName}'.",
                        task.Name,
                        dependency);
                    return Result<Unit, TaskExecutionError>.Failure(
                        new TaskExecutionError($"Unresolved dependency: '{dependency}'", task.Name));
                }
            }

            foreach (var dependency in task.Dependencies)
            {
                _dependencyGraph.AddDependency(task.Name, dependency);
            }

            _logger.Debug("[ExecutingEngine] Added task '{TaskName}' to the execution engine.", task.Name);
            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to add task '{TaskName}'.", task.Name);
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
            _logger.Debug("Executing task initialization '{TaskName}'.", task.Name);

            cancellationToken.ThrowIfCancellationRequested();

            var validationResult = await ValidateTaskAsync(task, executionScope).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            // Create the task to execute
            var executionTask = ExecuteWithRetryAsync(task, executionScope, cancellationToken);

            // Start tracking the task
            executionScope.Tracker.StartTask(task.Name, executionTask);

            var taskStartedMessage = TaskStartedMessage.Get(task.Name);
            try
            {
                await _taskStartedPublisher
                    .PublishAsync(_channelId, taskStartedMessage, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                TaskStartedMessage.Return(taskStartedMessage); // Return to pool
            }

            _logger.Debug("Starting task '{TaskName}'.", task.Name);

            // Await and capture the result of the execution
            var executionResult = await executionTask.ConfigureAwait(false);

            if (executionResult.IsSuccess)
            {
                var taskCompletedMessage = TaskCompletedMessage.Get(task.Name);
                try
                {
                    await _taskCompletedPublisher
                        .PublishAsync(_channelId, taskCompletedMessage, cancellationToken)
                        .ConfigureAwait(false);

                    executionScope.Tracker.CompleteTask(task.Name, executionResult.IsSuccess);
                }
                finally
                {
                    TaskCompletedMessage.Return(taskCompletedMessage); // Return to pool
                }

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
        var taskFailedMessage = TaskFailedMessage.Get(task.Name, error.Exception!);
        try
        {
            await _taskFailedPublisher
                .PublishAsync(_channelId, taskFailedMessage, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            TaskFailedMessage.Return(taskFailedMessage); // Return to pool
        }

        _logger.Error(error.Exception, "Task '{TaskName}' failed.", task.Name);

        if (task.CompensationActionAsync == null)
        {
            return Result<Unit, TaskExecutionError>.Failure(error);
        }

        var compensationResult =
            await ExecuteCompensationAsync(task, executionScope, cancellationToken)
                .ConfigureAwait(false); // Pass cancellationToken here
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
        ThrowIfDisposed();

        // Create a linked token that combines the disposal token and the provided cancellation token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposalCts.Token
        );

        // Use the linked token for execution
        return await ExecuteInternalAsync(linkedCts.Token).ConfigureAwait(false);
    }

    private async Task<Result<Unit, TaskExecutionError>> ExecuteInternalAsync(CancellationToken cancellationToken)
    {
        if (!PreExecutionChecks(out var preExecutionError))
        {
            return Result<Unit, TaskExecutionError>.Failure(preExecutionError ??
                                                            new TaskExecutionError("Unknown pre-execution error."));
        }

        using var rootScope = _scopeFactory.CreateScope();
        var executionScope = CreateExecutionScope(rootScope);

        try
        {
            var tasks = await ResolveDependenciesAsync().ConfigureAwait(false);
            if (!tasks.IsSuccess)
            {
                return Result<Unit, TaskExecutionError>.Failure(tasks.Error ??
                                                                new TaskExecutionError(
                                                                    "Unknown error resolving dependencies."));
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

        if (SafeIsExecuting)
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
        Tracker.SetTotalTaskCount(_tasks.Count);

        SafeIsExecuting = true;
        _logger.Information("Pre-execution checks passed. Starting task execution.");
        return true;
    }


    private TaskExecutionScope CreateExecutionScope(IServiceScope rootScope)
    {
        return new TaskExecutionScope(
            rootScope,
            Tracker,
            new ExecutionContext(_logger, _options, _scopeFactory) { TotalTaskCount = _tasks.Count },
            _logger);
    }

    private async Task<Result<Unit, TaskExecutionError>> ExecuteResolvedTasksAsync(
        List<ITask> tasks,
        TaskExecutionScope executionScope,
        CancellationToken cancellationToken)
    {
        _logger.Debug("Starting execution of resolved tasks. Total tasks: {TaskCount}. Caller: {Caller}",
            tasks.Count,
            GetCallerName());

        var result = await ExecuteTasksAsync(tasks, executionScope, cancellationToken).ConfigureAwait(false);
        await Tracker.WaitForAllTasksToCompleteAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        _logger.Debug("Execution of resolved tasks completed. Logging execution stats.");
        LogExecutionStats(executionScope.Tracker.GetStats());

        return result;
    }


    /// <summary>
    ///     Finalizes the execution by disposing of the execution scope.
    /// </summary>
    /// <param name="executionScope">The execution scope to dispose of.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task FinalizeExecutionAsync(TaskExecutionScope executionScope)
    {
        SafeIsExecuting = false;
        _logger.Information("Task execution completed.");

        try
        {
            await executionScope.DisposeAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Execution scope disposal was canceled.");
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
