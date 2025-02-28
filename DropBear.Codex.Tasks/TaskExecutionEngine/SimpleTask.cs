#region

using System.Runtime.CompilerServices;
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

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return false;
        }

        if (MaxRetryCount < 0)
        {
            return false;
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            return false;
        }

        if (Timeout <= TimeSpan.Zero)
        {
            return false;
        }

        return true;
    }

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);

        using var scope = await context.CreateScopeAsync(cts.Token).ConfigureAwait(false);

        try
        {
            await _executeAsync(context, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Task '{Name}' execution timed out after {Timeout.TotalSeconds} seconds");
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
