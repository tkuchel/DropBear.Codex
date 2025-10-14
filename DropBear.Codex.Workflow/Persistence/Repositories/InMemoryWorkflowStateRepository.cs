#region

using System.Collections.Concurrent;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;
using Serilog;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Repositories;

/// <summary>
///     Thread-safe in-memory implementation of workflow state repository.
/// </summary>
public sealed class InMemoryWorkflowStateRepository : IWorkflowStateRepository, IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, WorkflowStateWrapper> _states;
    private bool _disposed;

    public InMemoryWorkflowStateRepository(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _states = new ConcurrentDictionary<string, WorkflowStateWrapper>(StringComparer.Ordinal);
        _logger.Information("InMemoryWorkflowStateRepository initialized");
    }

    public int Count
    {
        get
        {
            ThrowIfDisposed();
            return _states.Count;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lock.Dispose();
        _states.Clear();
        _disposed = true;
        _logger.Information("InMemoryWorkflowStateRepository disposed");
    }

    public ValueTask<string> SaveWorkflowStateAsync<TContext>(
        WorkflowInstanceState<TContext> state,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(state);

        var wrapper = new WorkflowStateWrapper
        {
            ContextTypeName = typeof(TContext).FullName ?? typeof(TContext).Name, State = state
        };

        _lock.EnterWriteLock();
        try
        {
            if (!_states.TryAdd(state.WorkflowInstanceId, wrapper))
            {
                throw new InvalidOperationException(
                    $"Workflow instance {state.WorkflowInstanceId} already exists");
            }

            _logger.Debug(
                "Saved workflow state {InstanceId} for context type {ContextType}",
                state.WorkflowInstanceId,
                wrapper.ContextTypeName);

            return ValueTask.FromResult(state.WorkflowInstanceId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public ValueTask<WorkflowInstanceState<TContext>?> GetWorkflowStateAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);

        string requestedTypeName = typeof(TContext).FullName ?? typeof(TContext).Name;

        _lock.EnterReadLock();
        try
        {
            if (!_states.TryGetValue(workflowInstanceId, out WorkflowStateWrapper? wrapper))
            {
                _logger.Debug("Workflow state {InstanceId} not found", workflowInstanceId);
                return ValueTask.FromResult<WorkflowInstanceState<TContext>?>(null);
            }

            if (wrapper.ContextTypeName != requestedTypeName)
            {
                _logger.Warning(
                    "Type mismatch for workflow {InstanceId}: expected {ExpectedType}, found {ActualType}",
                    workflowInstanceId,
                    requestedTypeName,
                    wrapper.ContextTypeName);
                return ValueTask.FromResult<WorkflowInstanceState<TContext>?>(null);
            }

            if (wrapper.State is WorkflowInstanceState<TContext> typedState)
            {
                _logger.Debug("Retrieved workflow state {InstanceId}", workflowInstanceId);
                return ValueTask.FromResult<WorkflowInstanceState<TContext>?>(typedState);
            }

            _logger.Warning(
                "Failed to cast workflow state {InstanceId} to type {ContextType}",
                workflowInstanceId,
                requestedTypeName);
            return ValueTask.FromResult<WorkflowInstanceState<TContext>?>(null);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public ValueTask UpdateWorkflowStateAsync<TContext>(
        WorkflowInstanceState<TContext> state,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(state);

        string typeName = typeof(TContext).FullName ?? typeof(TContext).Name;

        var wrapper = new WorkflowStateWrapper { ContextTypeName = typeName, State = state };

        _lock.EnterWriteLock();
        try
        {
            _states[state.WorkflowInstanceId] = wrapper;

            _logger.Debug(
                "Updated workflow state {InstanceId} with status {Status}",
                state.WorkflowInstanceId,
                state.Status);

            return ValueTask.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public ValueTask DeleteWorkflowStateAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);

        _lock.EnterWriteLock();
        try
        {
            if (_states.TryRemove(workflowInstanceId, out _))
            {
                _logger.Information("Deleted workflow state {InstanceId}", workflowInstanceId);
            }
            else
            {
                _logger.Debug("Workflow state {InstanceId} not found for deletion", workflowInstanceId);
            }

            return ValueTask.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public ValueTask<IEnumerable<WorkflowInstanceState<TContext>>> GetWaitingWorkflowsAsync<TContext>(
        string? signalName = null,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ThrowIfDisposed();

        string requestedTypeName = typeof(TContext).FullName ?? typeof(TContext).Name;
        var waitingWorkflows = new List<WorkflowInstanceState<TContext>>();

        _lock.EnterReadLock();
        try
        {
            foreach (KeyValuePair<string, WorkflowStateWrapper> kvp in _states)
            {
                if (kvp.Value.ContextTypeName == requestedTypeName &&
                    kvp.Value.State is WorkflowInstanceState<TContext> typedState)
                {
                    if ((typedState.Status == WorkflowStatus.WaitingForSignal ||
                         typedState.Status == WorkflowStatus.WaitingForApproval) &&
                        (signalName == null || typedState.WaitingForSignal == signalName))
                    {
                        waitingWorkflows.Add(typedState);
                    }
                }
            }

            _logger.Debug(
                "Found {Count} waiting workflows for type {ContextType}" +
                (signalName != null ? $" and signal '{signalName}'" : ""),
                waitingWorkflows.Count,
                requestedTypeName);

            return ValueTask.FromResult<IEnumerable<WorkflowInstanceState<TContext>>>(waitingWorkflows);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class WorkflowStateWrapper
    {
        public required string ContextTypeName { get; init; }
        public required object State { get; init; }
    }
}
