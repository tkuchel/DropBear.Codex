#region

using System.Diagnostics;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskManagement;

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

    public TaskManager()
    {
        _logger = LoggerFactory.Logger.ForContext<TaskManager>();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void AddTask(string name, TaskDefinition taskDefinition, ExecutionOptions? options = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Task name cannot be null or empty.", nameof(name));
        }

        _tasks.Add((name, taskDefinition ?? throw new ArgumentNullException(nameof(taskDefinition)),
            options ?? new ExecutionOptions()));
    }

    public void AddConditionalBranch(string name, Func<TaskContext, Task<bool>> condition)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Branch name cannot be null or empty.", nameof(name));
        }

        _conditionalBranches[name] = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    public async Task<Result> ExecuteAsync(IProgress<(string TaskName, int ProgressPercentage)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        var context = new TaskContext { CancellationToken = linkedCts.Token, Logger = _logger };
        var totalTasks = _tasks.Count;
        var completedTasks = 0;

        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();

        foreach (var (name, taskDefinition, options) in _tasks)
        {
            await _pauseSemaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            try
            {
                while (_isPaused)
                {
                    _logger.Information("Execution paused before task: {TaskName}", name);
                    _pauseSemaphore.Release();
                    await Task.Delay(100, linkedCts.Token).ConfigureAwait(false);
                    await _pauseSemaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                }

                linkedCts.Token.ThrowIfCancellationRequested();
                _logger.Information("Evaluating task: {TaskName}", name);

                var shouldExecute = !_conditionalBranches.ContainsKey(name) ||
                                    await _conditionalBranches[name](context).ConfigureAwait(false);

                if (!shouldExecute)
                {
                    _logger.Information("Skipping task {TaskName} due to condition", name);
                    continue;
                }

                async Task ExecuteTask(CancellationTokenSource passedLinkedCts)
                {
                    var taskStopwatch = Stopwatch.StartNew();
                    var result = Result.Failure("Task did not execute successfully");

                    for (var retry = 0; retry <= options.MaxRetries; retry++)
                    {
                        try
                        {
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
                    _logger.Information("Task {TaskName} completed in {ElapsedMilliseconds}ms with result: {Success}",
                        name, taskStopwatch.ElapsedMilliseconds, result.IsSuccess);

                    if (!result.IsSuccess)
                    {
                        throw new InvalidOperationException($"Task {name} failed after {options.MaxRetries} retries");
                    }

                    Interlocked.Increment(ref completedTasks);
                    progress?.Report((name, completedTasks * 100 / totalTasks));
                }

                if (options.AllowParallel)
                {
                    tasks.Add(ExecuteTask(linkedCts));
                }
                else
                {
                    await ExecuteTask(linkedCts).ConfigureAwait(false);
                }
            }
            finally
            {
                _pauseSemaphore.Release();
            }
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        stopwatch.Stop();
        _logger.Information("All tasks completed in {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);

        return Result.Success();
    }

    public void Pause()
    {
        _isPaused = true;
        _logger.Information("Execution paused");
    }

    public void Resume()
    {
        _isPaused = false;
        _logger.Information("Execution resumed");
    }

    public void Cancel()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        _logger.Information("Execution cancelled");
    }

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
