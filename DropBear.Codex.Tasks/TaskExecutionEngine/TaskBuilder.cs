#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Fluent builder for constructing <see cref="ITask" /> instances with comprehensive configuration options.
///     Supports retry policies, timeouts, dependencies, compensation actions, and conditional execution.
/// </summary>
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

    /// <summary>
    ///     Creates a new <see cref="TaskBuilder" /> instance with the specified name.
    /// </summary>
    /// <param name="name">The unique name for the task.</param>
    /// <returns>A new TaskBuilder instance for fluent configuration.</returns>
    /// <exception cref="ArgumentException">Thrown when name is null or whitespace.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaskBuilder Create(string name)
    {
        return new TaskBuilder().WithName(name);
    }

    /// <summary>
    ///     Sets the name of the task being built.
    /// </summary>
    /// <param name="name">The unique name for the task.</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when name is null or whitespace.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        return this;
    }

    /// <summary>
    ///     Sets the asynchronous execution delegate for the task.
    /// </summary>
    /// <param name="executeAsync">The async delegate to execute when the task runs.</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when executeAsync is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithExecution(Func<ExecutionContext, CancellationToken, Task> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
        return this;
    }

    /// <summary>
    ///     Sets the synchronous execution action for the task.
    /// </summary>
    /// <param name="execute">The action to execute when the task runs.</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when execute is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithExecution(Action<ExecutionContext> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        return this;
    }

    /// <summary>
    ///     Sets the maximum number of retry attempts for the task if it fails.
    /// </summary>
    /// <param name="maxRetryCount">The maximum number of retries (0 = no retries).</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxRetryCount is negative.</exception>
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

    /// <summary>
    ///     Sets the delay between retry attempts.
    /// </summary>
    /// <param name="retryDelay">The time to wait between retries.</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when retryDelay is negative.</exception>
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

    /// <summary>
    ///     Sets the maximum execution timeout for the task.
    /// </summary>
    /// <param name="timeout">The maximum time allowed for task execution.</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when timeout is zero or negative.</exception>
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

    /// <summary>
    ///     Configures whether task execution should continue when this task fails.
    /// </summary>
    /// <param name="continueOnFailure">True to continue execution on failure; false to halt.</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder ContinueOnFailure(bool continueOnFailure = true)
    {
        _continueOnFailure = continueOnFailure;
        return this;
    }

    /// <summary>
    ///     Sets the task dependencies that must complete before this task can execute.
    /// </summary>
    /// <param name="dependencies">A collection of task names that this task depends on.</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when dependencies is null.</exception>
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

    /// <summary>
    ///     Sets a conditional predicate that determines whether the task should execute.
    /// </summary>
    /// <param name="condition">A function that returns true if the task should execute.</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when condition is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithCondition(Func<ExecutionContext, bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        _condition = condition;
        return this;
    }

    /// <summary>
    ///     Sets a compensation action to execute if the task fails (Saga pattern).
    /// </summary>
    /// <param name="compensationActionAsync">The async action to run for rollback/cleanup on failure.</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when compensationActionAsync is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithCompensationAction(Func<ExecutionContext, Task> compensationActionAsync)
    {
        ArgumentNullException.ThrowIfNull(compensationActionAsync);
        _compensationActionAsync = compensationActionAsync;
        return this;
    }

    /// <summary>
    ///     Adds a key-value metadata pair to the task for additional context or tracking.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when key is null or whitespace.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithMetadata(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    ///     Sets the execution priority for the task.
    /// </summary>
    /// <param name="priority">The task priority level (Low, Normal, High, Critical).</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskBuilder WithPriority(TaskPriority priority)
    {
        _priority = priority;
        return this;
    }

    /// <summary>
    ///     Sets the estimated duration for the task (for planning and monitoring purposes).
    /// </summary>
    /// <param name="estimatedDuration">The estimated time the task will take to complete.</param>
    /// <returns>This TaskBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when estimatedDuration is negative.</exception>
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

    /// <summary>
    ///     Validates that the builder configuration is complete and ready to build a task.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when required configuration (name or execution delegate) is missing.
    /// </exception>
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

    /// <summary>
    ///     Initializes the task with all configured properties from the builder.
    /// </summary>
    /// <param name="task">The SimpleTask instance to initialize.</param>
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
