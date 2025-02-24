#region

using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskManagement;

/// <summary>
///     <para>
///         Manages the execution of tasks (with optional parallelization, retry logic,
///         pause/resume, and cancellation).
///     </para>
///     <para>
///         <b>Obsolete:</b> This class has been replaced by <c>ExecutionEngine</c>
///         and will be removed in a future version.
///     </para>
/// </summary>
/// <remarks>
///     This class is marked obsolete. Consider using <c>ExecutionEngine</c> instead.
/// </remarks>
[Obsolete("Use the ExecutionEngine class instead. This TaskManager class will be removed in a future version.")]
public class TaskManager : IDisposable
{
    private readonly Dictionary<string, Func<TaskContext, Task<bool>>> _conditionalBranches =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger _logger;
    private readonly SemaphoreSlim _pauseSemaphore = new(1, 1);
    private readonly List<(string Name, TaskDefinition TaskDefinition, ExecutionOptions Options)> _tasks = new();

    private CancellationTokenSource _cts = new();
    private bool _isDisposed;
    private volatile bool _isPaused;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TaskManager" /> class.
    /// </summary>
    public TaskManager()
    {
        _logger = LoggerFactory.Logger.ForContext<TaskManager>();
    }

    /// <summary>
    ///     Releases the resources used by this <see cref="TaskManager" />.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Adds a task to the manager.
    /// </summary>
    /// <param name="name">The name of the task. Must not be null or empty.</param>
    /// <param name="taskDefinition">
    ///     A delegate defining the task execution logic. Must not be null.
    /// </param>
    /// <param name="options">
    ///     Optional <see cref="ExecutionOptions" />. If null, a default instance is used.
    /// </param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name" /> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="taskDefinition" /> is null.</exception>
    public void AddTask(string name, TaskDefinition taskDefinition, ExecutionOptions? options = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Task name cannot be null or empty.", nameof(name));
        }

        if (taskDefinition == null)
        {
            throw new ArgumentNullException(nameof(taskDefinition));
        }

        _tasks.Add((name, taskDefinition, options ?? new ExecutionOptions()));
    }

    /// <summary>
    ///     Adds a conditional branch that can dynamically skip or include a task based on runtime logic.
    /// </summary>
    /// <param name="name">The name of the branch condition. Must not be null or empty.</param>
    /// <param name="condition">
    ///     An asynchronous delegate returning <c>true</c> if the associated task should run; <c>false</c> otherwise.
    /// </param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name" /> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="condition" /> is null.</exception>
    public void AddConditionalBranch(string name, Func<TaskContext, Task<bool>> condition)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Branch name cannot be null or empty.", nameof(name));
        }

        _conditionalBranches[name] = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    /// <summary>
    ///     Executes all tasks in the manager, respecting the configured parallelization,
    ///     retry logic, conditional branches, pause/resume, and cancellation.
    /// </summary>
    /// <param name="progress">
    ///     An optional progress reporter that receives <c>(TaskName, ProgressPercentage)</c>.
    /// </param>
    /// <param name="cancellationToken">
    ///     An optional cancellation token that can cancel the entire operation.
    /// </param>
    /// <returns>A <see cref="Result" /> indicating success or failure of the execution.</returns>
    public async Task<Result> ExecuteAsync(
        IProgress<(string TaskName, int ProgressPercentage)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Combine the user-provided token with our internal token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

        var context = new TaskContext { CancellationToken = linkedCts.Token, Logger = _logger };

        var totalTasks = _tasks.Count;
        var completedTasks = 0;
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();

        // Iterate tasks in order
        foreach (var (name, taskDefinition, options) in _tasks)
        {
            // Acquire semaphore to control pause/resume
            await _pauseSemaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            try
            {
                // If paused, wait until resumed
                while (_isPaused)
                {
                    _logger.Information("Execution paused before task: {TaskName}", name);
                    _pauseSemaphore.Release();
                    await Task.Delay(100, linkedCts.Token).ConfigureAwait(false);
                    await _pauseSemaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                }

                // Check for cancellation
                linkedCts.Token.ThrowIfCancellationRequested();

                _logger.Information("Evaluating task: {TaskName}", name);

                // Evaluate branch condition if present
                var shouldExecute = !_conditionalBranches.ContainsKey(name) ||
                                    await _conditionalBranches[name](context).ConfigureAwait(false);

                if (!shouldExecute)
                {
                    _logger.Information("Skipping task {TaskName} due to condition", name);
                    continue;
                }

                // Local function to handle the task with retries
                async Task ExecuteTask(CancellationTokenSource passedLinkedCts)
                {
                    var taskStopwatch = Stopwatch.StartNew();
                    var result = Result.Failure("Task did not execute successfully");

                    for (var retry = 0; retry <= options.MaxRetries; retry++)
                    {
                        try
                        {
                            // Execute the actual task
                            result = await taskDefinition(context).ConfigureAwait(false);

                            if (result.IsSuccess)
                            {
                                break;
                            }

                            if (retry >= options.MaxRetries)
                            {
                                continue;
                            }

                            _logger.Warning(
                                "Task {TaskName} failed. Retrying in {RetryDelay}. Attempt {RetryCount} of {MaxRetries}",
                                name, options.RetryDelay, retry + 1, options.MaxRetries);

                            await Task.Delay(options.RetryDelay, passedLinkedCts.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not TaskCanceledException)
                        {
                            // Catch unhandled exceptions, consider them as failures, retry if still within limit
                            result = Result.Failure($"Task '{name}' threw an exception: {ex.Message}");

                            if (retry == options.MaxRetries)
                            {
                                break;
                            }

                            _logger.Warning(
                                "Task {TaskName} threw an exception. Retrying in {RetryDelay}. Attempt {RetryCount} of {MaxRetries}",
                                name, options.RetryDelay, retry + 1, options.MaxRetries);

                            await Task.Delay(options.RetryDelay, passedLinkedCts.Token).ConfigureAwait(false);
                        }
                    }

                    taskStopwatch.Stop();
                    _logger.Information(
                        "Task {TaskName} completed in {ElapsedMilliseconds}ms with result: {Success}",
                        name, taskStopwatch.ElapsedMilliseconds, result.IsSuccess);

                    if (!result.IsSuccess)
                    {
                        throw new InvalidOperationException(
                            $"Task {name} failed after {options.MaxRetries} retries");
                    }

                    // If success, increment completed tasks
                    Interlocked.Increment(ref completedTasks);

                    progress?.Report(
                        (name, completedTasks * 100 / totalTasks));
                }

                if (options.AllowParallel)
                {
                    // Add to a List of tasks for parallel execution
                    tasks.Add(ExecuteTask(linkedCts));
                }
                else
                {
                    // Run synchronously in the current iteration
                    await ExecuteTask(linkedCts).ConfigureAwait(false);
                }
            }
            finally
            {
                // Release the semaphore to let next tasks proceed
                _pauseSemaphore.Release();
            }
        }

        // Wait for any parallel tasks to finish
        await Task.WhenAll(tasks).ConfigureAwait(false);

        stopwatch.Stop();
        _logger.Information("All tasks completed in {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);

        return Result.Success();
    }

    /// <summary>
    ///     Pauses task execution (after the currently running task finishes).
    ///     Subsequent tasks will not start until <see cref="Resume" /> is called.
    /// </summary>
    public void Pause()
    {
        _isPaused = true;
        _logger.Information("Execution paused");
    }

    /// <summary>
    ///     Resumes task execution if currently paused.
    /// </summary>
    public void Resume()
    {
        _isPaused = false;
        _logger.Information("Execution resumed");
    }

    /// <summary>
    ///     Cancels all task execution, disposing the current <see cref="CancellationTokenSource" />.
    ///     Creates a new <see cref="CancellationTokenSource" /> for subsequent calls.
    /// </summary>
    public void Cancel()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        _logger.Information("Execution cancelled");
    }

    /// <summary>
    ///     Releases resources used by this class.
    /// </summary>
    /// <param name="disposing">
    ///     <c>true</c> to release both managed and unmanaged resources; <c>false</c> only for unmanaged.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            _cts.Dispose();
            _pauseSemaphore.Dispose();
        }

        _isDisposed = true;
    }
}
