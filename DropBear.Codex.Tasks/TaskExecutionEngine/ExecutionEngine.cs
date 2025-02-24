#region

using System.Collections.Concurrent;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine.Cache;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;
using TaskStatus = DropBear.Codex.Tasks.TaskExecutionEngine.Enums.TaskStatus;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Orchestrates the execution of tasks with dependency resolution, concurrency control, and status tracking.
/// </summary>
public sealed class ExecutionEngine : IAsyncDisposable
{
    private readonly BatchingProgressReporter _batchingReporter;
    private readonly TaskDependencyResolver _dependencyResolver;
    private readonly CancellationTokenSource _disposalCts;
    private readonly object _disposalLock = new();
    private readonly AsyncLock _executionLock;
    private readonly ILogger _logger;
    private readonly MessagePublisher _messagePublisher;
    private readonly ExecutionOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TaskPriorityQueue _taskQueue = new();
    private readonly ConcurrentDictionary<string, ITask> _tasks;
    private readonly ParallelTaskScheduler _taskScheduler;
    private readonly ValidationCache _validationCache;
    private bool _disposed;
    private int _isExecuting;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExecutionEngine" /> class.
    /// </summary>
    public ExecutionEngine(
        Guid channelId,
        IOptions<ExecutionOptions>? options,
        IServiceScopeFactory scopeFactory,
        IAsyncPublisher<Guid, TaskProgressMessage> progressPublisher,
        IAsyncPublisher<Guid, TaskStartedMessage> taskStartedPublisher,
        IAsyncPublisher<Guid, TaskCompletedMessage> taskCompletedPublisher,
        IAsyncPublisher<Guid, TaskFailedMessage> taskFailedPublisher)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = LoggerFactory.Logger.ForContext<ExecutionEngine>();

        _tasks = new ConcurrentDictionary<string, ITask>(StringComparer.Ordinal);
        _executionLock = new AsyncLock();
        _disposalCts = new CancellationTokenSource();
        Tracker = new TaskExecutionTracker();

        // Initialize message handling
        _messagePublisher = new MessagePublisher(
            progressPublisher ?? throw new ArgumentNullException(nameof(progressPublisher)),
            taskStartedPublisher ?? throw new ArgumentNullException(nameof(taskStartedPublisher)),
            taskCompletedPublisher ?? throw new ArgumentNullException(nameof(taskCompletedPublisher)),
            taskFailedPublisher ?? throw new ArgumentNullException(nameof(taskFailedPublisher))
        );

        _batchingReporter = new BatchingProgressReporter(
            _messagePublisher,
            channelId,
            _options.BatchingInterval);

        // Initialize optimization components
        _validationCache = new ValidationCache(_logger);
        _dependencyResolver = new TaskDependencyResolver(
            ObjectPoolProvider.StringSetPool,
            ObjectPoolProvider.TaskListPool);
        _taskScheduler = new ParallelTaskScheduler(
            _options.MaxDegreeOfParallelism ?? Environment.ProcessorCount,
            Tracker.GetStats(),
            new ConcurrentDictionary<string, TaskExecutionMetrics>(StringComparer.Ordinal));

        _logger.Debug("ExecutionEngine initialized with Channel ID: {ChannelId}", channelId);
    }

    /// <summary>
    ///     Indicates whether the engine is currently executing tasks.
    /// </summary>
    public bool IsExecuting => Interlocked.CompareExchange(ref _isExecuting, 0, 0) == 1;

    /// <summary>
    ///     Provides read-only access to the tasks managed by this engine.
    /// </summary>
    public IReadOnlyDictionary<string, ITask> Tasks => _tasks;

    /// <summary>
    ///     Tracks execution progress, durations, and statuses of tasks.
    /// </summary>
    public TaskExecutionTracker Tracker { get; }

    /// <inheritdoc />
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
            // Stop any ongoing operations
            await _disposalCts.CancelAsync().ConfigureAwait(false);

            // Give tasks up to 10 seconds to finish
            var trackerTask = Tracker.WaitForAllTasksToCompleteAsync(TimeSpan.FromSeconds(10));
            var messagePublisherTask = _messagePublisher.DisposeAsync().AsTask();
            var batchingReporterTask = _batchingReporter.DisposeAsync().AsTask();

            await Task.WhenAll(trackerTask, messagePublisherTask, batchingReporterTask)
                .ConfigureAwait(false);

            _tasks.Clear();
            Tracker.Reset();

            _executionLock.Dispose();
            _disposalCts.Dispose();
            _validationCache.Dispose();

            _logger.Information("ExecutionEngine disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during ExecutionEngine disposal");
        }
    }

    /// <summary>
    ///     Adds a task to the engine, provided execution has not started yet.
    /// </summary>
    public Result<Unit, TaskExecutionError> AddTask(ITask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        ThrowIfDisposed();

        try
        {
            if (IsExecuting)
            {
                return Result<Unit, TaskExecutionError>.Failure(
                    new TaskExecutionError("Cannot add tasks while execution is in progress"));
            }

            if (!_tasks.TryAdd(task.Name, task))
            {
                return Result<Unit, TaskExecutionError>.PartialSuccess(
                    Unit.Value,
                    new TaskExecutionError($"Task with name '{task.Name}' already exists", task.Name));
            }

            // Validate dependencies immediately
            var dependencyResult = ValidateDependencies(task);
            if (!dependencyResult.IsSuccess)
            {
                _tasks.TryRemove(task.Name, out _);
                return dependencyResult;
            }

            _logger.Debug("Added task '{TaskName}' with {DependencyCount} dependencies",
                task.Name, task.Dependencies.Count);

            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to add task '{TaskName}'", task.Name);
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Failed to add task", task.Name, ex));
        }
    }

    private Result<Unit, TaskExecutionError> ValidateDependencies(ITask task)
    {
        foreach (var dependency in task.Dependencies)
        {
            if (!_tasks.ContainsKey(dependency))
            {
                _logger.Warning("Task '{TaskName}' has an unresolved dependency: '{DependencyName}'",
                    task.Name, dependency);
                return Result<Unit, TaskExecutionError>.Failure(
                    new TaskExecutionError($"Unresolved dependency: '{dependency}'", task.Name));
            }
        }

        return Result<Unit, TaskExecutionError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Resolves dependencies and executes all tasks in the engine asynchronously.
    /// </summary>
    public async Task<Result<Unit, TaskExecutionError>> ExecuteAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var metrics = new ActivityScope("ExecuteEngine");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposalCts.Token);

        try
        {
            using (await _executionLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                // Ensure we don't start executing twice
                if (Interlocked.Exchange(ref _isExecuting, 1) == 1)
                {
                    return Result<Unit, TaskExecutionError>.Failure(
                        new TaskExecutionError("Execution already in progress"));
                }

                try
                {
                    // Resolve dependencies and queue tasks
                    var resolveResult = _dependencyResolver.ResolveDependencies(_tasks, out var orderedTasks);
                    if (!resolveResult.IsSuccess)
                    {
                        return resolveResult;
                    }

                    // Queue tasks in dependency order
                    foreach (var task in orderedTasks)
                    {
                        _taskQueue.Enqueue(task);
                    }

                    // Initialize execution context
                    using var rootScope = _scopeFactory.CreateScope();
                    var executionContext = new ExecutionContext(_options, _scopeFactory)
                    {
                        TotalTaskCount = _tasks.Count
                    };

                    Tracker.SetTotalTaskCount(_tasks.Count);

                    var executionScope = new TaskExecutionScope(
                        rootScope,
                        Tracker,
                        executionContext,
                        _logger);

                    // Execute tasks with parallel scheduler
                    var result = await _taskScheduler.ExecuteTasksAsync(
                        _taskQueue,
                        executionScope,
                        linkedCts.Token).ConfigureAwait(false);

                    metrics.Stop();
                    _logger.Information(
                        "Execution completed in {Duration}. Success: {IsSuccess}",
                        metrics.Elapsed,
                        result.IsSuccess);

                    return result;
                }
                finally
                {
                    _taskQueue.Clear();
                    Interlocked.Exchange(ref _isExecuting, 0);
                }
            }
        }
        catch (Exception ex)
        {
            metrics.Stop();
            _logger.Error(ex, "Execution failed unexpectedly after {Duration}", metrics.Elapsed);
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Execution failed unexpectedly", null, ex));
        }
    }

    /// <summary>
    ///     Removes all tasks from the engine, provided execution is not in progress.
    /// </summary>
    public Result<Unit, TaskExecutionError> ClearTasks()
    {
        if (IsExecuting)
        {
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Cannot clear tasks while execution is in progress"));
        }

        _tasks.Clear();
        _taskQueue.Clear();
        Tracker.Reset();
        return Result<Unit, TaskExecutionError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Reports progress for a specific task (if the engine is currently executing).
    ///     This enqueues the progress update into the <see cref="BatchingProgressReporter" />.
    /// </summary>
    public Task ReportProgressAsync(
        string taskName,
        double progress,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }

        if (!IsExecuting)
        {
            _logger.Warning("Cannot report progress when engine is not running");
            return Task.CompletedTask;
        }

        try
        {
            progress = Math.Clamp(progress, 0, 100);
            var stats = Tracker.GetStats();

            if (stats.TotalTasks == 0)
            {
                _logger.Error("Cannot report progress: total tasks not initialized");
                return Task.CompletedTask;
            }

            var progressMessage = ObjectPools<TaskProgressMessage>.Rent();
            try
            {
                progressMessage.Initialize(
                    taskName,
                    progress,
                    stats.CompletedTasks,
                    stats.TotalTasks,
                    TaskStatus.InProgress,
                    message ?? $"Task '{taskName}' progress");

                _ = _batchingReporter.QueueProgressUpdate(progressMessage);
            }
            finally
            {
                ObjectPools<TaskProgressMessage>.Return(progressMessage);
            }

            Tracker.UpdateLastProgress(taskName, progress);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to report progress for task '{TaskName}'", taskName);
        }

        return Task.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ExecutionEngine));
        }
    }
}
