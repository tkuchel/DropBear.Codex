#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

public sealed class TaskBuilder
{
    private readonly HashSet<string> _dependencies;
    private readonly Dictionary<string, object> _metadata;
    private Func<ExecutionContext, Task>? _compensationActionAsync;
    private Func<ExecutionContext, bool>? _condition;
    private bool _continueOnFailure;
    private TimeSpan _estimatedDuration = TimeSpan.Zero;
    private Action<ExecutionContext>? _execute;
    private Func<ExecutionContext, CancellationToken, Task>? _executeAsync;
    private int _maxRetryCount = 3;
    private string _name = string.Empty;
    private TaskPriority _priority = TaskPriority.Normal;
    private TimeSpan _retryDelay = TimeSpan.FromSeconds(1);
    private TimeSpan _timeout = TimeSpan.FromMinutes(5);

    private TaskBuilder()
    {
        _dependencies = new HashSet<string>(StringComparer.Ordinal);
        _metadata = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaskBuilder Create(string name)
    {
        return new TaskBuilder().WithName(name);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithExecution(Func<ExecutionContext, CancellationToken, Task> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithExecution(Action<ExecutionContext> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithMaxRetryCount(int maxRetryCount)
    {
        if (maxRetryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), "Max retry count cannot be negative.");
        }

        _maxRetryCount = maxRetryCount;
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithRetryDelay(TimeSpan retryDelay)
    {
        if (retryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay), "Retry delay cannot be negative.");
        }

        _retryDelay = retryDelay;
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
        }

        _timeout = timeout;
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder ContinueOnFailure(bool continueOnFailure = true)
    {
        _continueOnFailure = continueOnFailure;
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithDependencies(IEnumerable<string> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        foreach (var dep in dependencies)
        {
            if (!string.IsNullOrWhiteSpace(dep))
            {
                _dependencies.Add(dep);
            }
        }

        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithCondition(Func<ExecutionContext, bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        _condition = condition;
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithCompensationAction(Func<ExecutionContext, Task> compensationActionAsync)
    {
        ArgumentNullException.ThrowIfNull(compensationActionAsync);
        _compensationActionAsync = compensationActionAsync;
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithMetadata(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _metadata[key] = value;
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithPriority(TaskPriority priority)
    {
        _priority = priority;
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithEstimatedDuration(TimeSpan estimatedDuration)
    {
        if (estimatedDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedDuration), "Estimated duration cannot be negative.");
        }

        _estimatedDuration = estimatedDuration;
        return this;
    }

    /// <summary>
    ///     Builds the <see cref="SimpleTask" /> with the configured properties.
    /// </summary>
    public ITask Build()
    {
        ValidateConfiguration();

        var task = _executeAsync != null
            ? new SimpleTask(_name, _executeAsync)
            : new SimpleTask(_name, _execute!);

        InitializeTask(task);
        return task;
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            throw new InvalidOperationException("Task name is required.");
        }

        if (_executeAsync == null && _execute == null)
        {
            throw new InvalidOperationException("Execution delegate is required.");
        }
    }

    private void InitializeTask(SimpleTask task)
    {
        task.MaxRetryCount = _maxRetryCount;
        task.RetryDelay = _retryDelay;
        task.Timeout = _timeout;
        task.ContinueOnFailure = _continueOnFailure;
        task.Condition = _condition;
        task.Priority = _priority;
        task.EstimatedDuration = _estimatedDuration;

        if (_compensationActionAsync != null)
        {
            task.CompensationActionAsync = (context, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _compensationActionAsync(context);
            };
        }

        if (_dependencies.Count > 0)
        {
            task.SetDependencies(_dependencies);
        }

        foreach (var (key, value) in _metadata)
        {
            task.Metadata[key] = value;
        }
    }
}
