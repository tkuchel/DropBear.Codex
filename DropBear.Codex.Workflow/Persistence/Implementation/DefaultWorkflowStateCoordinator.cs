#region

using System.Reflection;
using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Implementation;

/// <summary>
///     Default implementation of workflow state coordination using reflection for dynamic type handling.
/// </summary>
public sealed partial class DefaultWorkflowStateCoordinator : IWorkflowStateCoordinator
{
    private readonly ILogger<DefaultWorkflowStateCoordinator> _logger;
    private readonly IWorkflowStateRepository _stateRepository;
    private readonly IWorkflowTypeResolver _typeResolver;

    /// <summary>
    ///     Initializes a new instance of the state coordinator.
    /// </summary>
    /// <param name="stateRepository">State repository for persistence</param>
    /// <param name="typeResolver">Type resolver for context types</param>
    /// <param name="logger">Logger instance</param>
    public DefaultWorkflowStateCoordinator(
        IWorkflowStateRepository stateRepository,
        IWorkflowTypeResolver typeResolver,
        ILogger<DefaultWorkflowStateCoordinator> logger)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask SaveWorkflowStateAsync<TContext>(
        WorkflowInstanceState<TContext> state,
        CancellationToken cancellationToken) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(state);
        await _stateRepository.SaveWorkflowStateAsync(state, cancellationToken).ConfigureAwait(false);
        LogWorkflowStateSaved(state.WorkflowInstanceId);
    }

    public async ValueTask<WorkflowInstanceState<TContext>?> LoadWorkflowStateAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken) where TContext : class
    {
        var state = await _stateRepository.GetWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken)
            .ConfigureAwait(false);

        if (state is not null)
        {
            LogWorkflowStateLoaded(workflowInstanceId);
        }
        else
        {
            LogWorkflowStateNotFound(workflowInstanceId);
        }

        return state;
    }

    public async ValueTask UpdateWorkflowStateAsync<TContext>(
        WorkflowInstanceState<TContext> state,
        CancellationToken cancellationToken) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(state);
        await _stateRepository.UpdateWorkflowStateAsync(state, cancellationToken).ConfigureAwait(false);
        LogWorkflowStateUpdated(state.WorkflowInstanceId);
    }

    public async ValueTask<(WorkflowStateInfo? StateInfo, Type? ContextType)> GetWorkflowStateInfoAsync(
        string workflowInstanceId,
        CancellationToken cancellationToken)
    {
        // Get the context type first
        Type? contextType = await GetWorkflowContextTypeAsync(workflowInstanceId, cancellationToken)
            .ConfigureAwait(false);

        if (contextType is null)
        {
            LogContextTypeNotFound(workflowInstanceId);
            return (null, null);
        }

        // Get the state with the correct type using reflection
        WorkflowStateInfo? stateInfo = await TryGetWorkflowStateInfoAsync(
            workflowInstanceId,
            contextType,
            cancellationToken).ConfigureAwait(false);

        return (stateInfo, stateInfo is not null ? contextType : null);
    }

    public async ValueTask<Type?> GetWorkflowContextTypeAsync(
        string workflowInstanceId,
        CancellationToken cancellationToken)
    {
        // Get type info directly from repository
        (string? assemblyQualifiedName, string? typeName) = await _stateRepository
            .GetWorkflowContextTypeInfoAsync(workflowInstanceId, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(assemblyQualifiedName) && string.IsNullOrEmpty(typeName))
        {
            LogCouldNotFindTypeInfo(workflowInstanceId);
            return null;
        }

        // Use type resolver to get the type
        Type? type = _typeResolver.ResolveContextType(assemblyQualifiedName, typeName);

        if (type is not null)
        {
            LogResolvedContextType(workflowInstanceId, type.FullName ?? type.Name);
        }

        return type;
    }

    private async ValueTask<WorkflowStateInfo?> TryGetWorkflowStateInfoAsync(
        string workflowInstanceId,
        Type contextType,
        CancellationToken cancellationToken)
    {
        try
        {
            MethodInfo? getStateMethod = typeof(IWorkflowStateRepository)
                .GetMethod(nameof(IWorkflowStateRepository.GetWorkflowStateAsync));

            if (getStateMethod is null)
            {
                LogGetStateMethodNotFound();
                return null;
            }

            MethodInfo genericMethod = getStateMethod.MakeGenericMethod(contextType);

            // Invoke the method - it returns ValueTask<WorkflowInstanceState<TContext>?>
            object? result = genericMethod.Invoke(
                _stateRepository,
                [workflowInstanceId, cancellationToken]);

            if (result is null)
            {
                return null;
            }

            // Handle ValueTask<T> properly
            Type resultType = result.GetType();
            if (!resultType.IsGenericType || resultType.GetGenericTypeDefinition() != typeof(ValueTask<>))
            {
                return null;
            }

            // Get the AsTask() method from ValueTask<T>
            MethodInfo? asTaskMethod = resultType.GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance);
            if (asTaskMethod is null)
            {
                return null;
            }

            // Convert ValueTask<T> to Task<T>
            var task = (Task?)asTaskMethod.Invoke(result, null);
            if (task is null)
            {
                return null;
            }

            await task.ConfigureAwait(false);

            // Get the Result property from Task<T>
            PropertyInfo? resultProperty = task.GetType().GetProperty("Result");
            object? state = resultProperty?.GetValue(task);

            if (state is null)
            {
                return null;
            }

            // Extract status and signal information using reflection
            PropertyInfo? statusProperty = state.GetType().GetProperty("Status");
            PropertyInfo? signalProperty = state.GetType().GetProperty("WaitingForSignal");

            if (statusProperty is null)
            {
                return null;
            }

            object? statusValue = statusProperty.GetValue(state);
            string? signalName = signalProperty?.GetValue(state) as string;

            if (statusValue is not WorkflowStatus status)
            {
                return null;
            }

            return new WorkflowStateInfo
            {
                WorkflowInstanceId = workflowInstanceId, Status = status, WaitingForSignal = signalName
            };
        }
        catch (TargetInvocationException)
        {
            // Type mismatch - not the right context type
            return null;
        }
        catch (Exception ex)
        {
            LogFailedToGetStateInfo(contextType.FullName ?? contextType.Name, workflowInstanceId, ex);
            return null;
        }
    }

    #region Logging

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Workflow state saved: {WorkflowInstanceId}")]
    partial void LogWorkflowStateSaved(string workflowInstanceId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Workflow state loaded: {WorkflowInstanceId}")]
    partial void LogWorkflowStateLoaded(string workflowInstanceId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Workflow state not found: {WorkflowInstanceId}")]
    partial void LogWorkflowStateNotFound(string workflowInstanceId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Workflow state updated: {WorkflowInstanceId}")]
    partial void LogWorkflowStateUpdated(string workflowInstanceId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Context type not found for workflow: {WorkflowInstanceId}")]
    partial void LogContextTypeNotFound(string workflowInstanceId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Could not find type info for workflow: {WorkflowInstanceId}")]
    partial void LogCouldNotFindTypeInfo(string workflowInstanceId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Resolved context type for workflow {WorkflowInstanceId}: {TypeName}")]
    partial void LogResolvedContextType(string workflowInstanceId, string typeName);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "GetStateAsync method not found on IWorkflowStateRepository")]
    partial void LogGetStateMethodNotFound();

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to get state info for context type {ContextType}, workflow {WorkflowInstanceId}")]
    partial void LogFailedToGetStateInfo(string contextType, string workflowInstanceId, Exception exception);

    #endregion
}
