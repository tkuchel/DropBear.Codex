#region

using System.Reflection;
using System.Text.Json;
using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;
using DropBear.Codex.Workflow.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Implementation;

/// <summary>
///     Persistent workflow engine that supports long-running workflows with state persistence and signals.
///     REFACTORED: Now uses specialized services for type resolution, state coordination, and signal handling.
/// </summary>
public sealed partial class PersistentWorkflowEngine : IPersistentWorkflowEngine
{
    private readonly IWorkflowEngine _baseEngine;
    private readonly ILogger<PersistentWorkflowEngine> _logger;
    private readonly IWorkflowNotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly DefaultWorkflowSignalHandler _signalHandler;
    private readonly IWorkflowStateCoordinator _stateCoordinator;

    /// <summary>
    ///     Initializes a new persistent workflow engine.
    /// </summary>
    /// <param name="baseEngine">Base workflow engine for execution</param>
    /// <param name="stateCoordinator">Coordinator for workflow state operations</param>
    /// <param name="notificationService">Service for workflow notifications</param>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="signalHandlerLogger">Logger for signal handler</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null</exception>
    public PersistentWorkflowEngine(
        IWorkflowEngine baseEngine,
        IWorkflowStateCoordinator stateCoordinator,
        IWorkflowNotificationService notificationService,
        IServiceProvider serviceProvider,
        ILogger<PersistentWorkflowEngine> logger,
        ILogger<DefaultWorkflowSignalHandler> signalHandlerLogger)
    {
        _baseEngine = baseEngine ?? throw new ArgumentNullException(nameof(baseEngine));
        _stateCoordinator = stateCoordinator ?? throw new ArgumentNullException(nameof(stateCoordinator));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create signal handler with callback to this engine's resume method
        _signalHandler = new DefaultWorkflowSignalHandler(
            stateCoordinator,
            ResumeWorkflowCallbackAsync,
            signalHandlerLogger ?? throw new ArgumentNullException(nameof(signalHandlerLogger)));
    }

    public async ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken) where TContext : class =>
        await _baseEngine.ExecuteAsync(definition, context, cancellationToken).ConfigureAwait(false);

    public async ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        WorkflowExecutionOptions options,
        CancellationToken cancellationToken) where TContext : class =>
        await _baseEngine.ExecuteAsync(definition, context, options, cancellationToken).ConfigureAwait(false);

    public async ValueTask<PersistentWorkflowResult<TContext>> StartPersistentWorkflowAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        string workflowInstanceId = Guid.NewGuid().ToString("N");

        // Capture context type information
        Type contextType = typeof(TContext);
        string contextTypeName = contextType.FullName ?? contextType.Name;
        string contextAssemblyQualifiedName = contextType.AssemblyQualifiedName ?? contextTypeName;

        var workflowState = new WorkflowInstanceState<TContext>
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowId = definition.WorkflowId,
            WorkflowDisplayName = definition.DisplayName,
            Context = context,
            Status = WorkflowStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            SerializedWorkflowDefinition = SerializeWorkflowDefinition(definition),

            // NEW: Store type information for fast deserialization
            ContextTypeName = contextTypeName,
            ContextAssemblyQualifiedName = contextAssemblyQualifiedName
        };

        await _stateCoordinator.SaveWorkflowStateAsync(workflowState, cancellationToken).ConfigureAwait(false);

        LogWorkflowStarted(definition.WorkflowId, workflowInstanceId);

        return await ContinueWorkflowExecutionAsync(workflowState, definition, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<PersistentWorkflowResult<TContext>> ResumeWorkflowAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken) where TContext : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);

        WorkflowInstanceState<TContext>? state =
            await _stateCoordinator.LoadWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken)
                .ConfigureAwait(false);

        if (state is null)
        {
            LogWorkflowNotFound(workflowInstanceId);
            throw new InvalidOperationException($"Workflow instance {workflowInstanceId} not found");
        }

        LogWorkflowResuming(workflowInstanceId);

        IWorkflowDefinition<TContext>? definition = DeserializeWorkflowDefinition<TContext>(
            state.SerializedWorkflowDefinition);

        if (definition is null)
        {
            LogWorkflowDefinitionDeserializationFailed(workflowInstanceId);
            throw new InvalidOperationException(
                $"Failed to deserialize workflow definition for instance {workflowInstanceId}");
        }

        return await ContinueWorkflowExecutionAsync(state, definition, cancellationToken).ConfigureAwait(false);
    }


    public async ValueTask<bool> CancelWorkflowAsync(
        string workflowInstanceId,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        LogWorkflowCancelling(workflowInstanceId, reason);

        (WorkflowStateInfo? stateInfo, Type? contextType) =
            await _stateCoordinator.GetWorkflowStateInfoAsync(workflowInstanceId, cancellationToken)
                .ConfigureAwait(false);

        if (stateInfo is null || contextType is null)
        {
            LogWorkflowNotFoundForCancellation(workflowInstanceId);
            return false;
        }

        // Update state to Cancelled using reflection
        bool updated = await UpdateWorkflowStatusToCancelledAsync(
            workflowInstanceId,
            reason,
            contextType,
            cancellationToken).ConfigureAwait(false);

        return updated;
    }

    public async ValueTask<bool> SignalWorkflowAsync<TData>(
        string workflowInstanceId,
        string signalName,
        TData? signalData,
        CancellationToken cancellationToken)
    {
        // Delegate to signal handler
        return await _signalHandler.DeliverSignalAsync(workflowInstanceId, signalName, signalData, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<WorkflowInstanceState<TContext>?> GetWorkflowStateAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken) where TContext : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);
        return await _stateCoordinator.LoadWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> ContinueWorkflowExecutionAsync<TContext>(
        WorkflowInstanceState<TContext> state,
        IWorkflowDefinition<TContext> definition,
        CancellationToken cancellationToken) where TContext : class
    {
        WorkflowResult<TContext> result =
            await _baseEngine.ExecuteAsync(definition, state.Context, cancellationToken).ConfigureAwait(false);

        return result.IsSuspended
            ? await HandleSuspendedWorkflowAsync(state, result, cancellationToken).ConfigureAwait(false)
            : await HandleCompletedWorkflowAsync(state, result, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> HandleSuspendedWorkflowAsync<TContext>(
        WorkflowInstanceState<TContext> state,
        WorkflowResult<TContext> result,
        CancellationToken cancellationToken) where TContext : class
    {
        WorkflowInstanceState<TContext> updatedState = state with
        {
            Status = WorkflowStatus.WaitingForSignal,
            WaitingForSignal = result.SuspendedSignalName,
            SignalTimeoutAt = DateTimeOffset.UtcNow.Add(WorkflowConstants.Defaults.DefaultSignalTimeout),
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await _stateCoordinator.UpdateWorkflowStateAsync(updatedState, cancellationToken).ConfigureAwait(false);

        LogWorkflowSuspended(state.WorkflowInstanceId, result.SuspendedSignalName);

        return new PersistentWorkflowResult<TContext>
        {
            WorkflowInstanceId = updatedState.WorkflowInstanceId,
            Status = WorkflowStatus.WaitingForSignal,
            Context = updatedState.Context,
            WaitingForSignal = result.SuspendedSignalName,
            SignalTimeoutAt = updatedState.SignalTimeoutAt
        };
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> HandleCompletedWorkflowAsync<TContext>(
        WorkflowInstanceState<TContext> state,
        WorkflowResult<TContext> result,
        CancellationToken cancellationToken) where TContext : class
    {
        WorkflowStatus finalStatus = result.IsSuccess ? WorkflowStatus.Completed : WorkflowStatus.Failed;

        WorkflowInstanceState<TContext> updatedState = state with
        {
            Status = finalStatus, CompletedAt = DateTimeOffset.UtcNow, LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await _stateCoordinator.UpdateWorkflowStateAsync(updatedState, cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await _notificationService.SendWorkflowCompletionNotificationAsync(
                updatedState,
                result,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _notificationService.SendWorkflowErrorNotificationAsync(
                updatedState,
                result.ErrorMessage ?? "Workflow execution failed",
                result.SourceException,
                cancellationToken).ConfigureAwait(false);
        }

        return new PersistentWorkflowResult<TContext>
        {
            WorkflowInstanceId = updatedState.WorkflowInstanceId,
            Status = finalStatus,
            Context = updatedState.Context,
            CompletionResult = result
        };
    }

    private static string SerializeWorkflowDefinition<TContext>(IWorkflowDefinition<TContext> definition)
        where TContext : class
    {
        var metadata = new
        {
            definition.WorkflowId,
            definition.DisplayName,
            Version = definition.Version.ToString(),
            definition.WorkflowTimeout,
            ContextType = typeof(TContext).AssemblyQualifiedName
        };

        return JsonSerializer.Serialize(metadata);
    }

    private IWorkflowDefinition<TContext>? DeserializeWorkflowDefinition<TContext>(string? serializedDefinition)
        where TContext : class
    {
        if (string.IsNullOrWhiteSpace(serializedDefinition))
        {
            return null;
        }

        try
        {
            // Attempt to resolve the workflow definition from the service provider
            using IServiceScope scope = _serviceProvider.CreateScope();
            return scope.ServiceProvider.GetService<IWorkflowDefinition<TContext>>();
        }
        catch (InvalidOperationException ex)
        {
            LogWorkflowDefinitionResolutionFailed(ex);
            return null;
        }
    }

    /// <summary>
    ///     Helper method used as a callback by DefaultWorkflowSignalHandler to resume workflows.
    /// </summary>
    private async ValueTask<bool> ResumeWorkflowCallbackAsync(
        string workflowInstanceId,
        Type contextType,
        CancellationToken cancellationToken)
    {
        try
        {
            MethodInfo? resumeMethod = GetType()
                .GetMethod(nameof(ResumeWorkflowAsync), BindingFlags.Public | BindingFlags.Instance);

            if (resumeMethod is null)
            {
                return false;
            }

            MethodInfo genericResumeMethod = resumeMethod.MakeGenericMethod(contextType);

            // ResumeWorkflowAsync returns ValueTask<PersistentWorkflowResult<TContext>>
            object? result = genericResumeMethod.Invoke(this, [workflowInstanceId, cancellationToken]);

            if (result is null)
            {
                return false;
            }

            // Handle ValueTask<T> properly
            Type resultType = result.GetType();
            MethodInfo? asTaskMethod = resultType.GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance);
            if (asTaskMethod is null)
            {
                return false;
            }

            var task = (Task?)asTaskMethod.Invoke(result, null);
            if (task is null)
            {
                return false;
            }

            await task.ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            LogWorkflowResumeFailed(workflowInstanceId, ex);
            return false;
        }
    }

    private async ValueTask<bool> UpdateWorkflowStatusToCancelledAsync(
        string workflowInstanceId,
        string reason,
        Type contextType,
        CancellationToken cancellationToken)
    {
        try
        {
            MethodInfo? getStateMethod = typeof(IWorkflowStateCoordinator)
                .GetMethod(nameof(IWorkflowStateCoordinator.LoadWorkflowStateAsync))
                ?.MakeGenericMethod(contextType);

            if (getStateMethod is null)
            {
                return false;
            }

            var stateTask = (Task?)getStateMethod.Invoke(_stateCoordinator, [workflowInstanceId, cancellationToken]);
            if (stateTask is null)
            {
                return false;
            }

            await stateTask.ConfigureAwait(false);
            object? workflowState = stateTask.GetType().GetProperty("Result")?.GetValue(stateTask);

            if (workflowState is null)
            {
                return false;
            }

            // Get the current metadata
            Type stateType = workflowState.GetType();
            var currentMetadata = stateType.GetProperty("Metadata")?.GetValue(workflowState)
                as IDictionary<string, object>;

            // Create updated metadata
            Dictionary<string, object> updatedMetadata = currentMetadata != null
                ? new Dictionary<string, object>(currentMetadata)
                : new Dictionary<string, object>();

            updatedMetadata["CancellationReason"] = reason;
            updatedMetadata["CancelledAt"] = DateTimeOffset.UtcNow;

            // Get properties
            PropertyInfo? statusProperty = stateType.GetProperty("Status");
            PropertyInfo? lastUpdatedAtProperty = stateType.GetProperty("LastUpdatedAt");
            PropertyInfo? metadataProperty = stateType.GetProperty("Metadata");

            if (statusProperty is null || lastUpdatedAtProperty is null || metadataProperty is null)
            {
                return false;
            }

            // Use reflection to get the 'with' method or create a new instance
            // For records, we need to use the copy constructor
            MethodInfo? cloneMethod = stateType.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance);

            object? updatedState;
            if (cloneMethod is not null)
            {
                // It's a record type, use the clone method
                updatedState = cloneMethod.Invoke(workflowState, null);
                if (updatedState is null)
                {
                    return false;
                }

                statusProperty.SetValue(updatedState, WorkflowStatus.Cancelled);
                lastUpdatedAtProperty.SetValue(updatedState, DateTimeOffset.UtcNow);
                metadataProperty.SetValue(updatedState, updatedMetadata);
            }
            else
            {
                // Not a record, modify properties directly
                statusProperty.SetValue(workflowState, WorkflowStatus.Cancelled);
                lastUpdatedAtProperty.SetValue(workflowState, DateTimeOffset.UtcNow);
                metadataProperty.SetValue(workflowState, updatedMetadata);
                updatedState = workflowState;
            }

            MethodInfo? updateStateMethod = typeof(IWorkflowStateCoordinator)
                .GetMethod(nameof(IWorkflowStateCoordinator.UpdateWorkflowStateAsync))
                ?.MakeGenericMethod(contextType);

            if (updateStateMethod is not null)
            {
                await ((Task)updateStateMethod.Invoke(_stateCoordinator, [updatedState, cancellationToken])!)
                    .ConfigureAwait(false);
            }

            LogWorkflowCancelled(workflowInstanceId, reason);
            return true;
        }
        catch (TargetInvocationException ex)
        {
            LogWorkflowCancellationError(workflowInstanceId, ex.InnerException ?? ex);
            return false;
        }
    }

    // Logger message source generators
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Started persistent workflow {WorkflowId} with instance {InstanceId}")]
    partial void LogWorkflowStarted(string workflowId, string instanceId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Workflow instance {InstanceId} not found for resumption")]
    partial void LogWorkflowNotFound(string instanceId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Resuming workflow instance {InstanceId}")]
    partial void LogWorkflowResuming(string instanceId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to deserialize workflow definition for instance {InstanceId}")]
    partial void LogWorkflowDefinitionDeserializationFailed(string instanceId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Cancelling workflow {InstanceId} with reason: {Reason}")]
    partial void LogWorkflowCancelling(string instanceId, string reason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Workflow instance {InstanceId} not found for cancellation")]
    partial void LogWorkflowNotFoundForCancellation(string instanceId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Workflow {InstanceId} suspended - waiting for signal: {SignalName}")]
    partial void LogWorkflowSuspended(string instanceId, string? signalName);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Cancelled workflow {InstanceId}: {Reason}")]
    partial void LogWorkflowCancelled(string instanceId, string reason);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error cancelling workflow {InstanceId}")]
    partial void LogWorkflowCancellationError(string instanceId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to resolve workflow definition from service provider")]
    partial void LogWorkflowDefinitionResolutionFailed(InvalidOperationException ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to resume workflow {InstanceId} via callback")]
    partial void LogWorkflowResumeFailed(string instanceId, Exception ex);
}
