#region

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
using Microsoft.Extensions.Options;
using Polly;
using Serilog;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;
using Stopwatch = System.Diagnostics.Stopwatch;
using TaskStatus = DropBear.Codex.Tasks.TaskExecutionEngine.Enums.TaskStatus;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

#endregion

/// <summary>
///     Represents the execution engine that manages and executes tasks with dependency resolution and retry logic.
/// </summary>
public sealed class ExecutionEngine : IAsyncDisposable, IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the ExecutionEngine class.
    /// </summary>
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

        _logger = LoggerFactory.Logger.ForContext<ExecutionEngine>();

        _messagePublisher = new MessagePublisher(
            progressPublisher ?? throw new ArgumentNullException(nameof(progressPublisher)),
            taskStartedPublisher ?? throw new ArgumentNullException(nameof(taskStartedPublisher)),
            taskCompletedPublisher ?? throw new ArgumentNullException(nameof(taskCompletedPublisher)),
            taskFailedPublisher ?? throw new ArgumentNullException(nameof(taskFailedPublisher))
        );

        _batchingReporter = new BatchingProgressReporter(
            _messagePublisher,
            _channelId,
            _options.BatchingInterval);

        StartMaintenanceTasks();

        _logger.Debug("ExecutionEngine instance created with Channel ID: {ChannelId}", _channelId);
    }


    /// <summary>
    ///     Asynchronously disposes of the execution engine and its resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger.Debug("ExecutionEngine disposal started.");

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
                await _disposalCts.CancelAsync().ConfigureAwait(false); // Cancel without awaiting to avoid deadlocks
            }

            // Ensure all tasks complete within the allowed timeframe
            var trackerTask = Tracker.WaitForAllTasksToCompleteAsync(TimeSpan.FromSeconds(10));
            var messagePublisherTask = _messagePublisher.DisposeAsync().AsTask();
            var batchingReporterTask = _batchingReporter.DisposeAsync().AsTask();

            // Wait for all resource cleanup tasks to complete
            await Task.WhenAll(trackerTask, messagePublisherTask, batchingReporterTask).ConfigureAwait(false);

            _logger.Debug("All cleanup tasks completed. Clearing internal state.");

            // Clear internal state
            _tasks.Clear();
            _dependencyInfo.Clear();
            Tracker.Reset();

            // Dispose locks and cancellation token
            _tasksLock.Dispose();
            _disposalCts.Dispose();

            _logger.Information("ExecutionEngine disposed successfully.");
        }
        catch (TaskCanceledException)
        {
            _logger.Warning("ExecutionEngine disposal was canceled.");
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
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void StartMaintenanceTasks()
    {
        // Start background task for validation cache cleanup
        _ = Task.Run(async () =>
        {
            while (!_disposed)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), _disposalCts.Token).ConfigureAwait(false);
                CleanupValidationCache();
            }
        }, _disposalCts.Token);
    }

    private void CleanupValidationCache()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _validationCache
            .Where(kvp => kvp.Value.ExpiresAt < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _validationCache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.Debug("Cleaned up {Count} expired validation cache entries", expiredKeys.Count);
        }
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
            _dependencyLock.EnterWriteLock();

            _logger.Debug("Adding task '{TaskName}' with dependencies: {Dependencies}", task.Name,
                string.Join(", ", task.Dependencies));


            if (IsExecuting)
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

            var info = _dependencyInfo.GetOrAdd(task.Name, _ => new DependencyInfo());
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

                // Check for circular dependency
                if (DetectCircularDependency(task.Name, dependency))
                {
                    _logger.Error("Circular dependency detected between task '{TaskName}' and '{DependencyName}'.",
                        task.Name, dependency);
                    return Result<Unit, TaskExecutionError>.Failure(
                        new TaskExecutionError($"Circular dependency detected between '{task.Name}' and '{dependency}'",
                            task.Name));
                }

                info.Dependencies.Add(dependency);
                _dependencyInfo.GetOrAdd(dependency, _ => new DependencyInfo())
                    .DependentTasks.Add(task.Name);

                _logger.Debug("Current dependency graph: {Graph}",
                    string.Join(", ",
                        _dependencyInfo.Select(d => $"{d.Key} -> {string.Join(", ", d.Value.Dependencies)}")));
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
        finally
        {
            _dependencyLock.ExitWriteLock();
        }
    }

    /// <summary>
    ///     Detects circular dependencies between tasks.
    /// </summary>
    /// <param name="taskName">The task being added.</param>
    /// <param name="dependencyName">The dependency being checked.</param>
    /// <returns>True if a circular dependency is detected; otherwise, false.</returns>
    private bool DetectCircularDependency(string taskName, string dependencyName)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(dependencyName);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (string.Equals(current, taskName, StringComparison.Ordinal))
            {
                return true; // Circular dependency detected
            }

            if (!visited.Add(current))
            {
                continue; // Skip already visited tasks
            }

            if (_dependencyInfo.TryGetValue(current, out var info))
            {
                foreach (var childDependency in info.Dependencies)
                {
                    stack.Push(childDependency);
                }
            }
        }

        return false;
    }


    /// <summary>
    ///     Clears all tasks from the execution engine.
    /// </summary>
    /// <returns>A result indicating the success or failure of the operation.</returns>
    public Result<Unit, TaskExecutionError> ClearTasks()
    {
        _logger.Debug("ClearTasks called by {Caller}.", GetCallerName());

        if (IsExecuting)
        {
            _logger.Warning("Cannot clear tasks while execution is in progress.");
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Cannot clear tasks while execution is in progress"));
        }

        if (_tasks.IsEmpty)
        {
            _logger.Debug("No tasks to clear from the execution engine.");
            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }

        ResetEngineState();
        _logger.Information("Cleared all tasks from the execution engine.");
        return Result<Unit, TaskExecutionError>.Success(Unit.Value);
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

        if (!IsExecuting)
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

            var lastProgress = Tracker.GetLastProgress(taskName);
            if (progress > lastProgress + 5.0 && progress - lastProgress < 50.0)
            {
                await SimulateSmoothProgressAsync(
                    taskName,
                    message ?? $"Task '{taskName}' execution progress.",
                    lastProgress,
                    progress,
                    500,
                    10,
                    cancellationToken
                ).ConfigureAwait(false);
            }
            else
            {
                var progressMessage = ObjectPools<TaskProgressMessage>.Rent();
                try
                {
                    progressMessage.Initialize(
                        taskName,
                        progress,
                        stats.CompletedTasks,
                        stats.TotalTasks,
                        TaskStatus.InProgress,
                        message ?? $"Task '{taskName}' execution progress.");

                    _batchingReporter.QueueProgressUpdate(progressMessage);
                }
                finally
                {
                    ObjectPools<TaskProgressMessage>.Return(progressMessage);
                }
            }

            Tracker.UpdateLastProgress(taskName, progress);
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
    ///     Simulates smooth progress transitions by generating intermediate progress updates.
    /// </summary>
    private async Task SimulateSmoothProgressAsync(
        string taskName,
        string message,
        double startProgress,
        double endProgress,
        int totalDurationMs,
        int totalSteps,
        CancellationToken cancellationToken)
    {
        if (startProgress >= endProgress || totalSteps <= 0)
        {
            return;
        }

        var messages = new TaskProgressMessage[totalSteps];
        var stats = Tracker.GetStats();
        var stepDelay = totalDurationMs / totalSteps;
        var progressIncrement = (endProgress - startProgress) / totalSteps;

        try
        {
            // Pre-initialize all messages
            for (var i = 0; i < totalSteps; i++)
            {
                messages[i] = MessagePools.ProgressMessagePool.Get();
                var currentProgress = startProgress + (i * progressIncrement);
                messages[i].Initialize(
                    taskName,
                    currentProgress,
                    stats.CompletedTasks,
                    stats.TotalTasks,
                    TaskStatus.InProgress,
                    message);
            }

            // Publish messages with delay
            for (var i = 0; i < totalSteps; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _messagePublisher.QueueMessage(_channelId, messages[i]);
                await Task.Delay(stepDelay, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            // Return all messages to pool
            foreach (var msg in messages)
            {
                if (msg != null!)
                {
                    MessagePools.ProgressMessagePool.Return(msg);
                }
            }
        }
    }

    /// <summary>
    ///     Resolves task dependencies and returns a list of tasks in execution order.
    /// </summary>
    private async Task<Result<List<ITask>, TaskExecutionError>> ResolveDependenciesAsync()
    {
        try
        {
            var sortedTasks = new List<ITask>(_tasks.Count);
            var visited = new Dictionary<string, TaskVisitState>(_tasks.Count, StringComparer.Ordinal);

            foreach (var taskPair in _tasks)
            {
                if (visited.ContainsKey(taskPair.Key))
                {
                    continue;
                }

                var visitResult = await VisitAsync(taskPair.Value, visited, sortedTasks).ConfigureAwait(false);
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
    private async Task<Result<Unit, TaskExecutionError>> VisitAsync(
        ITask task,
        Dictionary<string, TaskVisitState> visited,
        List<ITask> sortedTasks)
    {
        _logger.Debug("Visiting task: {TaskName}", task.Name);

        if (visited.TryGetValue(task.Name, out var state))
        {
            switch (state)
            {
                case TaskVisitState.Visiting:
                    _logger.Error("Circular dependency detected for task: {TaskName}. Dependency chain: {Chain}",
                        task.Name, GetDependencyChain(task.Name, visited));
                    return LogDependencyResolutionError(
                        $"Circular dependency detected for task '{task.Name}'.",
                        task.Name);
                case TaskVisitState.Visited:
                    _logger.Debug("Task '{TaskName}' already visited.", task.Name);
                    return Result<Unit, TaskExecutionError>.Success(Unit.Value);
                case TaskVisitState.NotVisited:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        visited[task.Name] = TaskVisitState.Visiting;
        _logger.Debug("Marked task '{TaskName}' as Visiting.", task.Name);

        if (_dependencyInfo.TryGetValue(task.Name, out var info))
        {
            if (info.Dependencies.Count == 0)
            {
                _logger.Debug("Task '{TaskName}' has no dependencies.", task.Name);
            }

            foreach (var dependencyName in info.Dependencies)
            {
                _logger.Debug("Task '{TaskName}' depends on '{DependencyName}'.", task.Name, dependencyName);

                if (!_tasks.TryGetValue(dependencyName, out var dependencyTask))
                {
                    _logger.Error("Task '{TaskName}' has an unresolved dependency: '{DependencyName}'.",
                        task.Name, dependencyName);
                    return LogDependencyResolutionError(
                        $"Task '{task.Name}' depends on an unknown task '{dependencyName}'.",
                        task.Name);
                }

                var visitResult = await VisitAsync(dependencyTask, visited, sortedTasks).ConfigureAwait(false);
                if (visitResult.IsSuccess)
                {
                    continue;
                }

                _logger.Error(
                    "Dependency resolution failed for task '{TaskName}' due to dependency '{DependencyName}'.",
                    task.Name,
                    dependencyName);
                return visitResult;
            }
        }

        visited[task.Name] = TaskVisitState.Visited;
        _logger.Debug("Marked task '{TaskName}' as Visited.", task.Name);

        if (_sortedTaskNames.Add(task.Name)) // Use HashSet for efficiency
        {
            sortedTasks.Add(task);
            _logger.Debug("Added task '{TaskName}' to sorted tasks.", task.Name);
        }

        return Result<Unit, TaskExecutionError>.Success(Unit.Value);
    }


    public async Task<Result<Unit, TaskExecutionError>> ExecuteAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var metrics = new Stopwatch();
        metrics.Start();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposalCts.Token
        );

        try
        {
            using (await _executionLock.LockAsync().ConfigureAwait(false))
            {
                if (Interlocked.Exchange(ref _isExecuting, 1) == 1)
                {
                    return
                        CreateError(TaskFailureReason.Unknown, "Execution already in progress");
                }

                try
                {
                    var tasks = await ResolveDependenciesAsync().ConfigureAwait(false);
                    if (!tasks.IsSuccess)
                    {
                        return Result<Unit, TaskExecutionError>.Failure(tasks.Error);
                    }

                    using var rootScope = _scopeFactory.CreateScope();
                    var executionContext = new ExecutionContext(_logger, _options, _scopeFactory)
                    {
                        TotalTaskCount = _tasks.Count
                    };

                    // Set total task count
                    Tracker.SetTotalTaskCount(_tasks.Count);

                    var executionScope = new TaskExecutionScope(
                        rootScope,
                        Tracker,
                        executionContext,
                        _logger);

                    var result = await ExecuteResolvedTasksAsync(tasks.Value!, executionScope, linkedCts.Token)
                        .ConfigureAwait(false);

                    metrics.Stop();
                    _logger.Information(
                        "Execution completed in {Duration}ms. Success: {IsSuccess}",
                        metrics.ElapsedMilliseconds,
                        result.IsSuccess);

                    return result;
                }
                finally
                {
                    Interlocked.Exchange(ref _isExecuting, 0);
                }
            }
        }
        catch (Exception ex)
        {
            metrics.Stop();
            _logger.Error(ex, "Execution failed unexpectedly after {Duration}ms", metrics.ElapsedMilliseconds);
            return
                CreateError(TaskFailureReason.Unknown, "Execution failed unexpectedly", ex);
        }
    }


    /// <summary>
    ///     Executes the resolved tasks either sequentially or in parallel based on configuration.
    /// </summary>
    private async Task<Result<Unit, TaskExecutionError>> ExecuteResolvedTasksAsync(
        List<ITask> tasks,
        TaskExecutionScope executionScope,
        CancellationToken cancellationToken)
    {
        var metrics = new Stopwatch();
        metrics.Start();

        try
        {
            _logger.Debug("Starting execution of {Count} tasks", tasks.Count);

            foreach (var task in tasks)
            {
                _taskQueue.Enqueue(task);
            }

            var result = EnableParallelExecution
                ? await ExecuteTasksParallelAsync(executionScope, cancellationToken).ConfigureAwait(false)
                : await ExecuteTasksSequentiallyAsync(executionScope, cancellationToken).ConfigureAwait(false);

            metrics.Stop();
            _logger.Debug("Task execution completed in {Duration}ms", metrics.ElapsedMilliseconds);

            await Tracker.WaitForAllTasksToCompleteAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            LogExecutionStats(executionScope.Tracker.GetStats());

            return result;
        }
        catch (Exception ex)
        {
            metrics.Stop();
            _logger.Error(ex, "Failed to execute tasks. Elapsed time: {Duration}ms", metrics.ElapsedMilliseconds);
            return
                CreateError(TaskFailureReason.Unknown, "Task execution failed unexpectedly", ex);
        }
    }

    /// <summary>
    ///     Executes tasks in parallel while respecting dependencies.
    /// </summary>
    private async Task<Result<Unit, TaskExecutionError>> ExecuteTasksParallelAsync(
        TaskExecutionScope executionScope,
        CancellationToken cancellationToken)
    {
        var taskStatus = ObjectPoolProvider.BoolDictionaryPool.Get();
        var failedTasks = new ConcurrentQueue<(string TaskName, Exception Exception)>();

        try
        {
            var batchSize = CalculateOptimalBatchSize(_tasks.Values.ToList());
            while (_taskQueue.TryDequeue(out var task))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var batch = new List<ITask>(batchSize) { task };

                while (batch.Count < batchSize && _taskQueue.TryDequeue(out var nextTask))
                {
                    batch.Add(nextTask);
                }

                var batchTasks = new List<Task<Result<Unit, TaskExecutionError>>>(batch.Count);

                foreach (var batchTask in batch)
                {
                    if (!_dependencyInfo.TryGetValue(batchTask.Name, out var info) ||
                        info.Dependencies.All(dep => taskStatus.GetValueOrDefault(dep)))
                    {
                        batchTasks.Add(ExecuteTaskAsync(batchTask, executionScope, cancellationToken));
                    }
                    else
                    {
                        taskStatus[batchTask.Name] = false;
                    }
                }

                try
                {
                    var results = await Task.WhenAll(batchTasks).ConfigureAwait(false);

                    foreach (var (batchTask, result) in batch.Zip(results, (t, r) => (t, r)))
                    {
                        taskStatus[batchTask.Name] = result.IsSuccess;
                        if (result is { IsSuccess: false, Error.Exception: not null })
                        {
                            failedTasks.Enqueue((batchTask.Name, result.Error.Exception));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error during parallel batch execution");
                    return CreateError(TaskFailureReason.Unknown, "Parallel batch execution failed", ex);
                }
            }

            if (failedTasks.IsEmpty)
            {
                return Result<Unit, TaskExecutionError>.Success(Unit.Value);
            }

            return Result<Unit, TaskExecutionError>.PartialSuccess(
                Unit.Value,
                CreateError(TaskFailureReason.ExecutionFailed,
                    $"Some tasks failed: {string.Join(", ", failedTasks.Select(f => f.TaskName))}").Error ??
                new TaskExecutionError("Some tasks failed"));
        }
        finally
        {
            ObjectPoolProvider.BoolDictionaryPool.Return(taskStatus);
        }
    }

    /// <summary>
    ///     Executes tasks sequentially in dependency order.
    /// </summary>
    private async Task<Result<Unit, TaskExecutionError>> ExecuteTasksSequentiallyAsync(
        TaskExecutionScope executionScope,
        CancellationToken cancellationToken)
    {
        var failedTasks = new List<(string TaskName, Exception Exception)>();

        while (_taskQueue.TryDequeue(out var task))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (_dependencyInfo.TryGetValue(task.Name, out var info) &&
                info.Dependencies.Any(dep =>
                    failedTasks.Any(t => string.Equals(t.TaskName, dep, StringComparison.Ordinal))))
            {
                _logger.Warning("Skipping task '{TaskName}' due to failed dependencies.", task.Name);
                failedTasks.Add((task.Name, new TaskSkippedException(task.Name)));
                continue;
            }

            var result = await ExecuteTaskAsync(task, executionScope, cancellationToken).ConfigureAwait(false);
            if (result is { IsSuccess: false, Error.Exception: not null })
            {
                failedTasks.Add((task.Name, result.Error.Exception));
            }
        }

        return failedTasks.Count == 0
            ? Result<Unit, TaskExecutionError>.Success(Unit.Value)
            : Result<Unit, TaskExecutionError>.PartialSuccess(
                Unit.Value,
                CreateError(TaskFailureReason.ExecutionFailed,
                    $"Some tasks failed: {string.Join(", ", failedTasks.Select(f => f.TaskName))}").Error ??
                new TaskExecutionError("Some tasks failed"));
    }

    /// <summary>
    ///     Executes a single task with retry logic and progress tracking.
    /// </summary>
    private async Task<Result<Unit, TaskExecutionError>> ExecuteTaskAsync(
        ITask task,
        TaskExecutionScope executionScope,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var metrics = new Stopwatch();
        metrics.Start();

        try
        {
            _logger.Debug("Starting execution of task '{TaskName}'", task.Name);
            cancellationToken.ThrowIfCancellationRequested();

            var validationResult = await ValidateTaskAsync(task, executionScope).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            var resources = new TaskResources();
            await using (resources.ConfigureAwait(false))
            {
                executionScope.Context.Resources = resources;

                var executionTask = ExecuteWithRetryAsync(task, executionScope, cancellationToken);
                executionScope.Tracker.StartTask(task.Name, executionTask);

                var startedMessage = MessagePools.StartedMessagePool.Get();
                try
                {
                    startedMessage.Initialize(task.Name);
                    _messagePublisher.QueueMessage(_channelId, startedMessage);
                }
                finally
                {
                    MessagePools.StartedMessagePool.Return(startedMessage);
                }

                var executionResult = await executionTask.ConfigureAwait(false);
                metrics.Stop();

                _taskMetrics.GetOrAdd(task.Name, _ => new TaskMetrics())
                    .RecordExecution(metrics.Elapsed, executionResult.IsSuccess);

                if (executionResult.IsSuccess)
                {
                    await UpdateProgressAsync(task, executionScope.Context, cancellationToken).ConfigureAwait(false);

                    _logger.Debug(
                        "Task '{TaskName}' completed successfully in {Duration}ms.",
                        task.Name, metrics.ElapsedMilliseconds);
                    return Result<Unit, TaskExecutionError>.Success(Unit.Value);
                }

                if (executionResult.Error?.Exception != null)
                {
                    return await HandleTaskFailureAsync(task, executionScope, executionResult.Error, cancellationToken)
                        .ConfigureAwait(false);
                }

                return executionResult;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            metrics.Stop();
            _logger.Error(ex,
                "Unexpected error executing task '{TaskName}' after {Duration}ms.",
                task.Name, metrics.ElapsedMilliseconds);
            executionScope.Tracker.CompleteTask(task.Name, false);
            return CreateError(TaskFailureReason.Unknown, "Unexpected error executing task", ex, task.Name);
        }
    }

    /// <summary>
    ///     Executes a task with retry logic using Polly.
    /// </summary>
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
    ///     Handles task failure and executes compensation if available.
    /// </summary>
    private async Task<Result<Unit, TaskExecutionError>> HandleTaskFailureAsync(
        ITask task,
        TaskExecutionScope executionScope,
        TaskExecutionError error,
        CancellationToken cancellationToken)
    {
        var failedMessage = MessagePools.FailedMessagePool.Get();
        try
        {
            failedMessage.Initialize(task.Name, error.Exception!);
            _messagePublisher.QueueMessage(_channelId, failedMessage);
        }
        finally
        {
            MessagePools.FailedMessagePool.Return(failedMessage);
        }

        _logger.Error(error.Exception, "Task '{TaskName}' failed.", task.Name);

        if (task.CompensationActionAsync == null)
        {
            return Result<Unit, TaskExecutionError>.Failure(error);
        }

        try
        {
            await task.CompensationActionAsync(executionScope.Context, cancellationToken).ConfigureAwait(false);
            return Result<Unit, TaskExecutionError>.Failure(error);
        }
        catch (Exception ex)
        {
            return Result<Unit, TaskExecutionError>.Failure(new TaskExecutionError(
                "Task failed and compensation action also failed",
                task.Name,
                new AggregateException(error.Exception!, ex)
            ));
        }
    }

    /// <summary>
    ///     Logs and returns a dependency resolution error.
    /// </summary>
    private Result<Unit, TaskExecutionError> LogDependencyResolutionError(string message, string taskName)
    {
        _logger.Error(message);
        return
            CreateError(TaskFailureReason.DependencyFailed, message, taskName: taskName);
    }

    /// <summary>
    ///     Updates task progress after completion.
    /// </summary>
    private async Task UpdateProgressAsync(ITask task, ExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            context.IncrementCompletedTaskCount();
            var progressMessage = MessagePools.ProgressMessagePool.Get();
            try
            {
                progressMessage.Initialize(
                    task.Name,
                    100,
                    context.CompletedTaskCount,
                    context.TotalTaskCount,
                    TaskStatus.Completed,
                    $"Task '{task.Name}' completed.");

                await Task.Run(() => _messagePublisher.QueueMessage(_channelId, progressMessage),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                MessagePools.ProgressMessagePool.Return(progressMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update progress for task '{TaskName}'", task.Name);
        }
    }

    /// <summary>
    ///     Validates a task before execution.
    /// </summary>
    private async Task<Result<Unit, TaskExecutionError>> ValidateTaskAsync(
        ITask task,
        TaskExecutionScope executionScope)
    {
        if (_validationCache.TryGetValue(task.Name, out var isValid))
        {
            if (isValid.IsValid)
            {
                return Result<Unit, TaskExecutionError>.Success(Unit.Value);
            }

            _logger.Warning("Skipping task '{TaskName}' due to previous validation failure.", task.Name);
            return Result<Unit, TaskExecutionError>.PartialSuccess(
                Unit.Value,
                new TaskExecutionError("Task skipped due to previous validation failure", task.Name));
        }

        var validationResult = Result<Unit, TaskExecutionError>.Success(Unit.Value);

        // Check async validation
        if (task is IAsyncValidatable asyncValidatable && !await asyncValidatable.ValidateAsync().ConfigureAwait(false))
        {
            validationResult = LogDependencyResolutionError("Async validation failed", task.Name);
        }
        // Check sync validation
        else if (!task.Validate())
        {
            validationResult = LogDependencyResolutionError("Validation failed", task.Name);
        }
        // Check conditions
        else if (task.Condition != null && !task.Condition(executionScope.Context))
        {
            _logger.Information("Skipping task '{TaskName}' due to condition.", task.Name);
            validationResult = Result<Unit, TaskExecutionError>.PartialSuccess(
                Unit.Value,
                new TaskExecutionError("Task skipped due to condition", task.Name));
        }

        _validationCache[task.Name] =
            (validationResult.IsSuccess, DateTime.UtcNow.AddMinutes(ValidationCacheExpiryMinutes));
        return validationResult;
    }

    /// <summary>
    ///     Represents dependency information for a task.
    /// </summary>
    private sealed class DependencyInfo
    {
        /// <summary>
        ///     Gets the set of dependencies for this task.
        /// </summary>
        public HashSet<string> Dependencies { get; } = new(StringComparer.Ordinal);

        /// <summary>
        ///     Gets the set of tasks that depend on this task.
        /// </summary>
        public HashSet<string> DependentTasks { get; } = new(StringComparer.Ordinal);

        /// <summary>
        ///     Gets or sets the last validation time for dependencies.
        /// </summary>
        public DateTime LastValidated { get; set; } = DateTime.UtcNow;
    }

    #region Fields

    private readonly AsyncLock _executionLock = new();
    private readonly BatchingProgressReporter _batchingReporter;
    private readonly Guid _channelId;
    private readonly ConcurrentDictionary<string, DependencyInfo> _dependencyInfo = new(StringComparer.Ordinal);
    private readonly ReaderWriterLockSlim _dependencyLock = new();
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly object _disposalLock = new();
    private readonly ILogger _logger;
    private readonly MessagePublisher _messagePublisher;
    private readonly ExecutionOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TaskPriorityQueue _taskQueue = new();
    private readonly ConcurrentDictionary<string, TaskMetrics> _taskMetrics = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ITask> _tasks = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _tasksLock = new(1, 1);
    private readonly HashSet<string> _sortedTaskNames = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, (bool IsValid, DateTime ExpiresAt)> _validationCache =
        new(StringComparer.Ordinal);

    private bool _disposed;
    private int _isExecuting;

    // Constants
    private const int ValidationCacheExpiryMinutes = 30;
    private const int DefaultBatchSize = 5;

    #endregion

    #region Properties

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

    #endregion

    #region Utility Methods

    private string GetDependencyChain(string taskName, Dictionary<string, TaskVisitState> visited)
    {
        var chain = new List<string>();
        foreach (var kvp in visited.Where(kvp => kvp.Value == TaskVisitState.Visiting))
        {
            chain.Add(kvp.Key);
        }

        chain.Add(taskName); // Add the current task to complete the chain
        return string.Join(" -> ", chain);
    }


    private int CalculateOptimalBatchSize(IReadOnlyCollection<ITask> tasks)
    {
        if (!tasks.Any())
        {
            return DefaultBatchSize;
        }

        var averageExecutionTime = tasks
            .Select(t => _taskMetrics.GetValueOrDefault(t.Name)?.GetStats().AverageExecutionTime ?? TimeSpan.Zero)
            .Average(t => t.TotalMilliseconds);

        return Math.Max(1, Math.Min(
            _options.ParallelBatchSize,
            (int)(_options.ParallelBatchSize * (1000 / Math.Max(1, averageExecutionTime)))
        ));
    }

    private Result<Unit, TaskExecutionError> CreateError(
        TaskFailureReason reason,
        string message,
        Exception? exception = null,
        string? taskName = null)
    {
        var error = new TaskExecutionError(
            $"{reason}: {message}",
            taskName ?? "Unknown",
            exception);

        return Result<Unit, TaskExecutionError>.Failure(error);
    }

    private void ResetEngineState()
    {
        _tasks.Clear();
        _taskQueue.Clear();
        _dependencyInfo.Clear();
        _taskMetrics.Clear();
        _validationCache.Clear();
        Tracker.Reset();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ExecutionEngine));
        }
    }

    private void ValidateTaskName(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }
    }

    private static string GetCallerName([CallerMemberName] string callerName = "")
    {
        return callerName;
    }

    private void LogExecutionStats(TaskExecutionStats stats)
    {
        _logger.Information(
            "Task Execution Summary: Total={TotalTasks}, Completed={CompletedTasks}, " +
            "Failed={FailedTasks}, Skipped={SkippedTasks}",
            stats.TotalTasks,
            stats.CompletedTasks,
            stats.FailedTasks,
            stats.SkippedTasks);

        foreach (var duration in stats.TaskDurations)
        {
            _logger.Debug("Task '{TaskName}' completed in {Duration:g}.", duration.Key, duration.Value);
        }
    }

    #endregion
}
