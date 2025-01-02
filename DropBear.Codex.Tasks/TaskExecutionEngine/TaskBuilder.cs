#region

using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Provides a builder for creating tasks with optional configurations.
/// </summary>
public class TaskBuilder
{
    private readonly HashSet<string> _dependencies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);
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

    /// <summary>
    ///     Creates a new instance of the <see cref="TaskBuilder" /> with the specified task name.
    /// </summary>
    /// <param name="name">The unique name of the task.</param>
    /// <returns>A new instance of <see cref="TaskBuilder" />.</returns>
    public static TaskBuilder Create(string name)
    {
        return new TaskBuilder().WithName(name);
    }

    /// <summary>
    ///     Sets the name of the task.
    /// </summary>
    /// <param name="name">The unique name of the task.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
    public TaskBuilder WithName(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        return this;
    }

    /// <summary>
    ///     Sets the asynchronous execution logic of the task.
    /// </summary>
    /// <param name="executeAsync">The asynchronous execution delegate.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
    public TaskBuilder WithExecution(Func<ExecutionContext, CancellationToken, Task> executeAsync)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        return this;
    }

    /// <summary>
    ///     Sets the synchronous execution logic of the task.
    /// </summary>
    /// <param name="execute">The synchronous execution delegate.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
    public TaskBuilder WithExecution(Action<ExecutionContext> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        return this;
    }

    /// <summary>
    ///     Sets the maximum retry count for the task.
    /// </summary>
    /// <param name="maxRetryCount">The maximum number of retry attempts.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
    public TaskBuilder WithMaxRetryCount(int maxRetryCount)
    {
        if (maxRetryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), "Max retry count cannot be negative.");
        }

        _maxRetryCount = maxRetryCount;
        return this;
    }

    /// <summary>
    ///     Sets the retry delay for the task.
    /// </summary>
    /// <param name="retryDelay">The delay between retry attempts.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
    public TaskBuilder WithRetryDelay(TimeSpan retryDelay)
    {
        if (retryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay), "Retry delay cannot be negative.");
        }

        _retryDelay = retryDelay;
        return this;
    }

    /// <summary>
    ///     Sets whether to continue on failure.
    /// </summary>
    /// <param name="continueOnFailure">Whether to continue on failure.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
    public TaskBuilder ContinueOnFailure(bool continueOnFailure = true)
    {
        _continueOnFailure = continueOnFailure;
        return this;
    }

    /// <summary>
    ///     Adds dependencies for the task.
    /// </summary>
    /// <param name="dependencies">An enumerable of task names that this task depends on.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
    public TaskBuilder WithDependencies(IEnumerable<string> dependencies)
    {
        if (dependencies == null)
        {
            throw new ArgumentNullException(nameof(dependencies));
        }

        foreach (var dependency in dependencies)
        {
            if (!string.IsNullOrWhiteSpace(dependency))
            {
                _dependencies.Add(dependency);
            }
        }

        return this;
    }

    /// <summary>
    ///     Sets the condition under which the task should execute.
    /// </summary>
    /// <param name="condition">The condition delegate.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
    public TaskBuilder WithCondition(Func<ExecutionContext, bool> condition)
    {
        _condition = condition;
        return this;
    }

    /// <summary>
    ///     Sets the compensation action for the task.
    /// </summary>
    /// <param name="compensationActionAsync">The compensation action delegate.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
    public TaskBuilder WithCompensationAction(Func<ExecutionContext, Task> compensationActionAsync)
    {
        _compensationActionAsync = compensationActionAsync;
        return this;
    }

    /// <summary>
    ///     Adds metadata for the task.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
    public TaskBuilder WithMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Metadata key cannot be null or whitespace.", nameof(key));
        }

        _metadata[key] = value;
        return this;
    }

    /// <summary>
    ///     Sets the priority of the task.
    /// </summary>
    /// <param name="priority">The task priority.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
    public TaskBuilder WithPriority(TaskPriority priority)
    {
        _priority = priority;
        return this;
    }

    /// <summary>
    ///     Sets the estimated duration of the task.
    /// </summary>
    /// <param name="estimatedDuration">The estimated duration of the task.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
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
    ///     Sets the timeout for the task execution.
    /// </summary>
    /// <param name="timeout">The timeout for the task execution.</param>
    /// <returns>The current <see cref="TaskBuilder" /> instance.</returns>
    public TaskBuilder WithTimeout(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout cannot be negative.");
        }

        _timeout = timeout;
        return this;
    }

    /// <summary>
    ///     Builds the task with the specified configurations.
    /// </summary>
    /// <returns>An instance of <see cref="ITask" />.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required configurations are missing.</exception>
    public ITask Build()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            throw new InvalidOperationException("Task name is required.");
        }

        if (_executeAsync == null && _execute == null)
        {
            throw new InvalidOperationException("Execution delegate is required.");
        }

        var task = _executeAsync != null
            ? new SimpleTask(_name, _executeAsync)
            : new SimpleTask(_name, _execute!);

        task.MaxRetryCount = _maxRetryCount;
        task.RetryDelay = _retryDelay;
        task.ContinueOnFailure = _continueOnFailure;
        task.Condition = _condition;
        task.Priority = _priority;
        task.EstimatedDuration = _estimatedDuration;
        task.Timeout = _timeout;

        // Wrap compensation action
        task.CompensationActionAsync = _compensationActionAsync == null
            ? null
            : (context, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _compensationActionAsync(context);
            };

        if (_dependencies.Any())
        {
            task.SetDependencies(_dependencies);
        }

        foreach (var (key, value) in _metadata)
        {
            task.Metadata[key] = value;
        }

        return task;
    }
}
