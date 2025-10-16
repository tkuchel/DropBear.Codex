#region

using System.Collections.Concurrent;
using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Repositories;

/// <summary>
///     In-memory implementation of workflow state repository for testing and development.
///     WARNING: This implementation does not persist state across application restarts.
/// </summary>
public sealed partial class InMemoryWorkflowStateRepository : IWorkflowStateRepository, IDisposable
{
    private readonly ILogger<InMemoryWorkflowStateRepository> _logger;
    private readonly ConcurrentDictionary<string, WorkflowStateWrapper> _states;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new in-memory workflow state repository.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null</exception>
    public InMemoryWorkflowStateRepository(ILogger<InMemoryWorkflowStateRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _states = new ConcurrentDictionary<string, WorkflowStateWrapper>(StringComparer.OrdinalIgnoreCase);

        LogUsingInMemoryRepository();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        LogDisposing(_states.Count);
        _states.Clear();
        _disposed = true;
    }

    /// <inheritdoc />
    public ValueTask<WorkflowInstanceState<TContext>?> GetWorkflowStateAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);

        if (_states.TryGetValue(workflowInstanceId, out WorkflowStateWrapper? wrapper))
        {
            string requestedTypeName = typeof(TContext).FullName ?? typeof(TContext).Name;

            if (string.Equals(wrapper.ContextTypeName, requestedTypeName, StringComparison.OrdinalIgnoreCase))
            {
                if (wrapper.State is WorkflowInstanceState<TContext> typedState)
                {
                    LogRetrievedWorkflowState(workflowInstanceId);
                    return ValueTask.FromResult<WorkflowInstanceState<TContext>?>(typedState);
                }

                LogCouldNotCastState(workflowInstanceId, requestedTypeName);
            }
            else
            {
                LogTypeMismatch(workflowInstanceId, wrapper.ContextTypeName, requestedTypeName);
            }
        }

        LogWorkflowStateNotFound(workflowInstanceId);
        return ValueTask.FromResult<WorkflowInstanceState<TContext>?>(null);
    }

    /// <inheritdoc />
    public ValueTask<string> SaveWorkflowStateAsync<TContext>(
        WorkflowInstanceState<TContext> state,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.WorkflowInstanceId);

        string typeName = typeof(TContext).FullName ?? typeof(TContext).Name;
        var wrapper = new WorkflowStateWrapper { ContextTypeName = typeName, State = state };

        _states.AddOrUpdate(
            state.WorkflowInstanceId,
            wrapper,
            (_, _) => wrapper);

        LogSavedWorkflowState(state.WorkflowInstanceId, state.Status);

        return ValueTask.FromResult(state.WorkflowInstanceId);
    }

    /// <inheritdoc />
    public ValueTask UpdateWorkflowStateAsync<TContext>(
        WorkflowInstanceState<TContext> state,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.WorkflowInstanceId);

        string typeName = typeof(TContext).FullName ?? typeof(TContext).Name;
        var wrapper = new WorkflowStateWrapper { ContextTypeName = typeName, State = state };

        _states.AddOrUpdate(
            state.WorkflowInstanceId,
            wrapper,
            (_, _) => wrapper);

        LogUpdatedWorkflowState(state.WorkflowInstanceId, state.Status);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DeleteWorkflowStateAsync(
        string workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);

        if (_states.TryRemove(workflowInstanceId, out _))
        {
            LogDeletedWorkflowState(workflowInstanceId);
        }
        else
        {
            LogWorkflowStateNotFoundForDeletion(workflowInstanceId);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IEnumerable<WorkflowInstanceState<TContext>>> GetWaitingWorkflowsAsync<TContext>(
        string? signalName = null,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ThrowIfDisposed();

        string requestedTypeName = typeof(TContext).FullName ?? typeof(TContext).Name;
        var waitingWorkflows = new List<WorkflowInstanceState<TContext>>();

        foreach (KeyValuePair<string, WorkflowStateWrapper> kvp in _states)
        {
            if (string.Equals(kvp.Value.ContextTypeName, requestedTypeName, StringComparison.OrdinalIgnoreCase) &&
                kvp.Value.State is WorkflowInstanceState<TContext> typedState)
            {
                if ((typedState.Status == WorkflowStatus.WaitingForSignal ||
                     typedState.Status == WorkflowStatus.WaitingForApproval) &&
                    (signalName == null ||
                     string.Equals(typedState.WaitingForSignal, signalName, StringComparison.OrdinalIgnoreCase)))
                {
                    waitingWorkflows.Add(typedState);
                }
            }
        }

        LogFoundWaitingWorkflows(waitingWorkflows.Count, requestedTypeName, signalName ?? string.Empty);

        return ValueTask.FromResult<IEnumerable<WorkflowInstanceState<TContext>>>(waitingWorkflows);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>
    ///     Internal wrapper to store workflow state with its context type information.
    /// </summary>
    private sealed class WorkflowStateWrapper
    {
        public required string ContextTypeName { get; init; }
        public required object State { get; init; }
    }

    #region LoggerMessage Delegates

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Using InMemoryWorkflowStateRepository - state will not persist across application restarts")]
    private partial void LogUsingInMemoryRepository();

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Disposing InMemoryWorkflowStateRepository with {Count} stored states")]
    private partial void LogDisposing(int count);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Retrieved workflow state {InstanceId}")]
    private partial void LogRetrievedWorkflowState(string instanceId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Workflow state {InstanceId} found but could not be cast to {TypeName}")]
    private partial void LogCouldNotCastState(string instanceId, string typeName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Workflow state {InstanceId} has type {ActualType} but {RequestedType} was requested")]
    private partial void LogTypeMismatch(string instanceId, string actualType, string requestedType);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Workflow state {InstanceId} not found")]
    private partial void LogWorkflowStateNotFound(string instanceId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Saved workflow state {InstanceId} with status {Status}")]
    private partial void LogSavedWorkflowState(string instanceId, WorkflowStatus status);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Updated workflow state {InstanceId} with status {Status}")]
    private partial void LogUpdatedWorkflowState(string instanceId, WorkflowStatus status);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Deleted workflow state {InstanceId}")]
    private partial void LogDeletedWorkflowState(string instanceId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Workflow state {InstanceId} not found for deletion")]
    private partial void LogWorkflowStateNotFoundForDeletion(string instanceId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Found {Count} waiting workflows for type {ContextType}{SignalFilter}")]
    private partial void LogFoundWaitingWorkflows(int count, string contextType, string signalFilter);

    #endregion
}
