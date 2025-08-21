using DropBear.Codex.Workflow.Persistence.Implementation;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;
using Microsoft.Extensions.Logging;

namespace DropBear.Codex.Workflow.Persistence.Repositories;

/// <summary>
/// FIXED: Enhanced in-memory workflow state repository with proper type handling and debugging
/// </summary>
public class InMemoryWorkflowStateRepository : IWorkflowStateRepository
{
    // Store by workflow instance ID -> (state object, context type name)
    private readonly Dictionary<string, (object State, string ContextTypeName)> _states = new();
    private readonly Lock _lock = new Lock();
    private readonly ILogger<PersistentWorkflowEngine> _logger;
    public ValueTask<string> SaveWorkflowStateAsync<TContext>(WorkflowInstanceState<TContext> state, CancellationToken cancellationToken = default) where TContext : class
    {
        lock (_lock)
        {
            var contextTypeName = typeof(TContext).FullName ?? typeof(TContext).Name;
            _states[state.WorkflowInstanceId] = (state, contextTypeName);
            _logger.LogDebug($"  🔍 REPO: Saved workflow {state.WorkflowInstanceId} with context type {contextTypeName}");
        }
        return ValueTask.FromResult(state.WorkflowInstanceId);
    }

    public ValueTask<WorkflowInstanceState<TContext>?> GetWorkflowStateAsync<TContext>(string workflowInstanceId, CancellationToken cancellationToken = default) where TContext : class
    {
        lock (_lock)
        {
            if (!_states.TryGetValue(workflowInstanceId, out var stateInfo))
            {
                _logger.LogDebug($"  🔍 REPO: Workflow {workflowInstanceId} not found in repository");
                return ValueTask.FromResult<WorkflowInstanceState<TContext>?>(null);
            }

            var requestedTypeName = typeof(TContext).FullName ?? typeof(TContext).Name;
            _logger.LogDebug($"  🔍 REPO: Found workflow {workflowInstanceId}, stored type: {stateInfo.ContextTypeName}, requested: {requestedTypeName}");

            // Check if the stored type matches the requested type
            if (stateInfo.ContextTypeName == requestedTypeName && stateInfo.State is WorkflowInstanceState<TContext> typedState)
            {
                _logger.LogDebug($"  🔍 REPO: Direct cast successful to {requestedTypeName}");
                return ValueTask.FromResult<WorkflowInstanceState<TContext>?>(typedState);
            }

            _logger.LogDebug($"  🔍 REPO: Type mismatch - cannot cast {stateInfo.ContextTypeName} to {requestedTypeName}");
            return ValueTask.FromResult<WorkflowInstanceState<TContext>?>(null);
        }
    }

    public ValueTask UpdateWorkflowStateAsync<TContext>(WorkflowInstanceState<TContext> state, CancellationToken cancellationToken = default) where TContext : class
    {
        lock (_lock)
        {
            var contextTypeName = typeof(TContext).FullName ?? typeof(TContext).Name;
            _states[state.WorkflowInstanceId] = (state, contextTypeName);
            _logger.LogDebug($"  🔍 REPO: Updated workflow {state.WorkflowInstanceId} with context type {contextTypeName}");
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteWorkflowStateAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _states.Remove(workflowInstanceId);
            _logger.LogDebug($"  🔍 REPO: Deleted workflow {workflowInstanceId}");
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<IEnumerable<WorkflowInstanceState<TContext>>> GetWaitingWorkflowsAsync<TContext>(string? signalName = null, CancellationToken cancellationToken = default) where TContext : class
    {
        lock (_lock)
        {
            var waitingWorkflows = new List<WorkflowInstanceState<TContext>>();
            var requestedTypeName = typeof(TContext).FullName ?? typeof(TContext).Name;

            foreach (var kvp in _states)
            {
                if (kvp.Value.ContextTypeName == requestedTypeName &&
                    kvp.Value.State is WorkflowInstanceState<TContext> typedState)
                {
                    if ((typedState.Status == WorkflowStatus.WaitingForSignal || typedState.Status == WorkflowStatus.WaitingForApproval) &&
                        (signalName == null || typedState.WaitingForSignal == signalName))
                    {
                        waitingWorkflows.Add(typedState);
                    }
                }
            }

            _logger.LogDebug($"  🔍 REPO: Found {waitingWorkflows.Count} waiting workflows for type {requestedTypeName}");
            return ValueTask.FromResult<IEnumerable<WorkflowInstanceState<TContext>>>(waitingWorkflows);
        }
    }

    /// <summary>
    /// FIXED: Added missing method to get all workflow states regardless of type (for debugging and type discovery)
    /// </summary>
    public IEnumerable<(string WorkflowId, string ContextType, object State)> GetAllWorkflowStates()
    {
        lock (_lock)
        {
            return _states.Select(kvp => (kvp.Key, kvp.Value.ContextTypeName, kvp.Value.State)).ToList();
        }
    }

    /// <summary>
    /// FIXED: Added method to get all context type names currently in the repository
    /// </summary>
    public IEnumerable<string> GetAllContextTypeNames()
    {
        lock (_lock)
        {
            return _states.Values.Select(v => v.ContextTypeName).Distinct().ToList();
        }
    }

    /// <summary>
    /// FIXED: Added method to check if a workflow exists with any context type
    /// </summary>
    public bool WorkflowExists(string workflowInstanceId)
    {
        lock (_lock)
        {
            return _states.ContainsKey(workflowInstanceId);
        }
    }
}
