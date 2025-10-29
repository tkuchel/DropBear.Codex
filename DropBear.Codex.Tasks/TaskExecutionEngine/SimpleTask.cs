#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Represents a simple task that can be executed within the execution engine.
///     Holds minimal state and optional dependencies, plus a synchronous or asynchronous delegate.
/// </summary>
public sealed class SimpleTask : ITask
{
    private readonly HashSet<string> _dependencies;
    private readonly Func<ExecutionContext, CancellationToken, Task> _executeAsync;
    private Dictionary<string, object>? _metadata;

    public SimpleTask(string name, Func<ExecutionContext, CancellationToken, Task> executeAsync)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _dependencies = new HashSet<string>(StringComparer.Ordinal);
    }

    public SimpleTask(string name, Action<ExecutionContext> execute)
        : this(name, WrapSyncExecution(execute))
    {
    }

    public string Name { get; }
    public Func<ExecutionContext, bool>? Condition { get; set; }
    public int MaxRetryCount { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public bool ContinueOnFailure { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public Func<ExecutionContext, CancellationToken, Task>? CompensationActionAsync { get; set; }

    public IReadOnlyList<string> Dependencies => _dependencies.ToList();

    public IDictionary<string, object> Metadata => _metadata ??= new Dictionary<string, object>(StringComparer.Ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddDependency(string dependency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependency);
        _dependencies.Add(dependency);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetDependencies(IEnumerable<string> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        _dependencies.Clear();
        foreach (var dep in dependencies.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            _dependencies.Add(dep);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveDependency(string dependency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependency);
        _dependencies.Remove(dependency);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasDependency(string dependency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependency);
        return _dependencies.Contains(dependency);
    }

    public Result<Unit, TaskValidationError> Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return Result<Unit, TaskValidationError>.Failure(
                TaskValidationError.InvalidName(Name ?? string.Empty));
        }

        if (MaxRetryCount < 0)
        {
            return Result<Unit, TaskValidationError>.Failure(
                TaskValidationError.InvalidProperty(Name, nameof(MaxRetryCount), "must be non-negative"));
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            return Result<Unit, TaskValidationError>.Failure(
                TaskValidationError.InvalidProperty(Name, nameof(RetryDelay), "must be non-negative"));
        }

        if (Timeout <= TimeSpan.Zero)
        {
            return Result<Unit, TaskValidationError>.Failure(
                TaskValidationError.InvalidProperty(Name, nameof(Timeout), "must be positive"));
        }

        return Result<Unit, TaskValidationError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, TaskExecutionError>> ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken)
    {
        if (context == null)
        {
            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Execution context cannot be null", Name));
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);

        try
        {
            using var scope = await context.CreateScopeAsync(cts.Token).ConfigureAwait(false);
            await _executeAsync(context, cts.Token).ConfigureAwait(false);
            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<Unit, TaskExecutionError>.Failure(
                TaskExecutionError.Timeout(Name, Timeout));
        }
        catch (OperationCanceledException)
        {
            return Result<Unit, TaskExecutionError>.Failure(
                TaskExecutionError.Cancelled(Name));
        }
        catch (Exception ex)
        {
            return Result<Unit, TaskExecutionError>.Failure(
                TaskExecutionError.Failed(Name, ex));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<ExecutionContext, CancellationToken, Task> WrapSyncExecution(Action<ExecutionContext> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return async (context, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            execute(context);
            await Task.CompletedTask.ConfigureAwait(false);
        };
    }
}
