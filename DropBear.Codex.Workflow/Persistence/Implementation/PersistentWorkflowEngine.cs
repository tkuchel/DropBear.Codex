#region

using System.Collections.Concurrent;
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
/// </summary>
public sealed partial class PersistentWorkflowEngine : IPersistentWorkflowEngine
{
    private readonly IWorkflowEngine _baseEngine;
    private readonly Lazy<ConcurrentDictionary<string, Type>> _knownWorkflowContextTypes;
    private readonly ILogger<PersistentWorkflowEngine> _logger;
    private readonly IWorkflowNotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkflowStateRepository _stateRepository;

    /// <summary>
    ///     Initializes a new persistent workflow engine.
    /// </summary>
    /// <param name="baseEngine">Base workflow engine for execution</param>
    /// <param name="stateRepository">Repository for workflow state persistence</param>
    /// <param name="notificationService">Service for workflow notifications</param>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    /// <param name="logger">Logger instance</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null</exception>
    public PersistentWorkflowEngine(
        IWorkflowEngine baseEngine,
        IWorkflowStateRepository stateRepository,
        IWorkflowNotificationService notificationService,
        IServiceProvider serviceProvider,
        ILogger<PersistentWorkflowEngine> logger)
    {
        _baseEngine = baseEngine ?? throw new ArgumentNullException(nameof(baseEngine));
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _knownWorkflowContextTypes = new Lazy<ConcurrentDictionary<string, Type>>(DiscoverWorkflowContextTypes);
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

        await _stateRepository.SaveWorkflowStateAsync(workflowState, cancellationToken).ConfigureAwait(false);

        LogWorkflowStarted(definition.WorkflowId, workflowInstanceId);

        return await ContinueWorkflowExecutionAsync(workflowState, definition, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<PersistentWorkflowResult<TContext>> ResumeWorkflowAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken) where TContext : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);

        WorkflowInstanceState<TContext>? state =
            await _stateRepository.GetWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken)
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
            await FindWorkflowStateInfoAsync(workflowInstanceId, cancellationToken).ConfigureAwait(false);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        LogWorkflowSignaling(workflowInstanceId, signalName);

        (WorkflowStateInfo? stateInfo, Type? contextType) =
            await FindWorkflowStateInfoAsync(workflowInstanceId, cancellationToken).ConfigureAwait(false);

        if (stateInfo is null || contextType is null)
        {
            LogWorkflowNotFoundForSignaling(workflowInstanceId);
            return false;
        }

        if (!IsValidStateForSignaling(stateInfo, signalName, workflowInstanceId))
        {
            return false;
        }

        return await ResumeWorkflowWithSignalAsync(workflowInstanceId, contextType, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<WorkflowInstanceState<TContext>?> GetWorkflowStateAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken) where TContext : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);
        return await _stateRepository.GetWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets the context type from a workflow instance using stored type metadata.
    ///     OPTIMIZED: No longer iterates through all types.
    /// </summary>
    private async ValueTask<Type?> GetWorkflowContextTypeAsync(
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

        // Try assembly qualified name first (most reliable)
        if (!string.IsNullOrEmpty(assemblyQualifiedName))
        {
            var type = Type.GetType(assemblyQualifiedName);
            if (type is not null)
            {
                LogResolvedContextTypeFromMetadata(workflowInstanceId, type.FullName ?? type.Name);
                return type;
            }
        }

        // Fallback: try to find by type name in known types
        if (!string.IsNullOrEmpty(typeName))
        {
            ConcurrentDictionary<string, Type> contextTypes = _knownWorkflowContextTypes.Value;
            if (contextTypes.TryGetValue(typeName, out Type? knownType))
            {
                LogResolvedContextTypeFromKnownTypes(workflowInstanceId, typeName);
                return knownType;
            }
        }

        LogCouldNotResolveContextType(workflowInstanceId);
        return null;
    }

    /// <summary>
    ///     Helper method that wraps ResumeWorkflowAsync in a Task-returning method for reflection compatibility.
    /// </summary>
    private async Task<bool> ResumeWorkflowGenericAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken) where TContext : class
    {
        try
        {
            await ResumeWorkflowAsync<TContext>(workflowInstanceId, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            LogWorkflowResumeError(workflowInstanceId, ex);
            return false;
        }
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

        await _stateRepository.UpdateWorkflowStateAsync(updatedState, cancellationToken).ConfigureAwait(false);

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

        await _stateRepository.UpdateWorkflowStateAsync(updatedState, cancellationToken).ConfigureAwait(false);

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

    private ConcurrentDictionary<string, Type> DiscoverWorkflowContextTypes()
    {
        var contextTypes = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        try
        {
            LogDiscoveringWorkflowContextTypes();

            Assembly[] assemblies =
            [
                .. AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !IsSystemAssembly(a))
            ];

            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    Type[] types =
                    [
                        .. assembly.GetTypes()
                            .Where(IsWorkflowContextType)
                    ];

                    foreach (Type type in types)
                    {
                        string key = type.FullName ?? type.Name;
                        _ = contextTypes.TryAdd(key, type);
                        LogDiscoveredContextType(key);
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    LogFailedToLoadTypes(assembly.FullName ?? "Unknown", ex);
                }
            }

            LogDiscoveredContextTypeCount(contextTypes.Count);
        }
        catch (ReflectionTypeLoadException ex)
        {
            LogContextTypeDiscoveryError(ex);
        }

        return contextTypes;
    }

    private static bool IsWorkflowContextType(Type type)
    {
        // More restrictive filtering to avoid internal types
        if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
        {
            return false;
        }

        // Exclude compiler-generated types
        if (type.Name.Contains("<") || type.Name.Contains(">"))
        {
            return false;
        }

        // Exclude WinRT types
        if (type.FullName?.StartsWith("WinRT.", StringComparison.Ordinal) == true)
        {
            return false;
        }

        // Exclude internal runtime types
        if (type.FullName?.Contains("+<") == true || type.FullName?.Contains(">d__") == true)
        {
            return false;
        }

        string? ns = type.Namespace;
        if (ns is null)
        {
            return false;
        }

        // Only include types from non-system namespaces
        if (ns.StartsWith("System", StringComparison.Ordinal) ||
            ns.StartsWith("Microsoft", StringComparison.Ordinal) ||
            ns.StartsWith("Windows", StringComparison.Ordinal) ||
            ns.StartsWith("WinRT", StringComparison.Ordinal) ||
            ns.StartsWith("Internal", StringComparison.Ordinal))
        {
            return false;
        }

        // Must have at least one public property
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Length > 0;
    }

    private static bool IsSystemAssembly(Assembly assembly)
    {
        string assemblyName = assembly.GetName().Name ?? string.Empty;
        return assemblyName.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Finds workflow state information using stored type metadata instead of iterating all types.
    /// </summary>
    private async ValueTask<(WorkflowStateInfo?, Type?)> FindWorkflowStateInfoAsync(
        string workflowInstanceId,
        CancellationToken cancellationToken)
    {
        // NEW APPROACH: Get the context type directly
        Type? contextType = await GetWorkflowContextTypeAsync(workflowInstanceId, cancellationToken)
            .ConfigureAwait(false);

        if (contextType is null)
        {
            LogWorkflowStateNotFound(workflowInstanceId);
            return (null, null);
        }

        // Now get the state with the correct type
        WorkflowStateInfo? stateInfo = await TryGetWorkflowStateInfoAsync(
            workflowInstanceId,
            contextType,
            cancellationToken).ConfigureAwait(false);

        return (stateInfo, stateInfo is not null ? contextType : null);
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
                return null;
            }

            MethodInfo genericMethod = getStateMethod.MakeGenericMethod(contextType);

            // Invoke the method - it returns ValueTask<WorkflowInstanceState<TContext>?>
            object? result = genericMethod.Invoke(
                _stateRepository,
                new object[] { workflowInstanceId, cancellationToken });

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
            LogFailedToCheckType(contextType.FullName ?? contextType.Name, workflowInstanceId, ex);
            return null;
        }
    }

    private bool IsValidStateForSignaling(WorkflowStateInfo stateInfo, string signalName, string workflowInstanceId)
    {
        if (stateInfo.Status != WorkflowStatus.WaitingForSignal &&
            stateInfo.Status != WorkflowStatus.WaitingForApproval)
        {
            LogWorkflowNotWaitingForSignal(workflowInstanceId, stateInfo.Status);
            return false;
        }

        if (!string.Equals(stateInfo.WaitingForSignal, signalName, StringComparison.OrdinalIgnoreCase))
        {
            LogSignalMismatch(workflowInstanceId, stateInfo.WaitingForSignal, signalName);
            return false;
        }

        return true;
    }

    private async ValueTask<bool> ResumeWorkflowWithSignalAsync(
        string workflowInstanceId,
        Type contextType,
        CancellationToken cancellationToken)
    {
        MethodInfo? resumeMethod = GetType()
            .GetMethod(nameof(ResumeWorkflowGenericAsync), BindingFlags.NonPublic | BindingFlags.Instance);

        if (resumeMethod is null)
        {
            LogResumeMethodNotFound();
            return false;
        }

        MethodInfo genericResumeMethod = resumeMethod.MakeGenericMethod(contextType);
        try
        {
            // ResumeWorkflowGenericAsync returns Task<bool>, which we can safely cast
            var resumeTask = (Task<bool>?)genericResumeMethod.Invoke(this, [workflowInstanceId, cancellationToken]);

            if (resumeTask is not null)
            {
                return await resumeTask.ConfigureAwait(false);
            }

            return false;
        }
        catch (TargetInvocationException ex)
        {
            LogWorkflowResumeError(workflowInstanceId, ex.InnerException ?? ex);
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
            MethodInfo? getStateMethod = typeof(IWorkflowStateRepository)
                .GetMethod(nameof(IWorkflowStateRepository.GetWorkflowStateAsync))
                ?.MakeGenericMethod(contextType);

            if (getStateMethod is null)
            {
                return false;
            }

            var stateTask = (Task?)getStateMethod.Invoke(_stateRepository, [workflowInstanceId, cancellationToken]);
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

            MethodInfo? updateStateMethod = typeof(IWorkflowStateRepository)
                .GetMethod(nameof(IWorkflowStateRepository.UpdateWorkflowStateAsync))
                ?.MakeGenericMethod(contextType);

            if (updateStateMethod is not null)
            {
                await ((Task)updateStateMethod.Invoke(_stateRepository, [updatedState, cancellationToken])!)
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
        Message = "Signaling workflow {InstanceId} with signal {SignalName}")]
    partial void LogWorkflowSignaling(string instanceId, string signalName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Workflow instance {InstanceId} not found for signaling")]
    partial void LogWorkflowNotFoundForSignaling(string instanceId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Workflow {InstanceId} suspended - waiting for signal: {SignalName}")]
    partial void LogWorkflowSuspended(string instanceId, string? signalName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Workflow instance {InstanceId} is not waiting for a signal (current status: {Status})")]
    partial void LogWorkflowNotWaitingForSignal(string instanceId, WorkflowStatus status);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message =
            "Workflow instance {InstanceId} is waiting for signal {ExpectedSignal}, but received {ReceivedSignal}")]
    partial void LogSignalMismatch(string instanceId, string? expectedSignal, string receivedSignal);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error resuming workflow {InstanceId} after signal")]
    partial void LogWorkflowResumeError(string instanceId, Exception ex);

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
        Level = LogLevel.Debug,
        Message = "Discovering workflow context types from loaded assemblies")]
    partial void LogDiscoveringWorkflowContextTypes();

    [LoggerMessage(
        Level = LogLevel.Trace,
        Message = "Discovered workflow context type: {TypeName}")]
    partial void LogDiscoveredContextType(string typeName);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Failed to load types from assembly {AssemblyName}")]
    partial void LogFailedToLoadTypes(string assemblyName, ReflectionTypeLoadException ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Discovered {Count} workflow context types")]
    partial void LogDiscoveredContextTypeCount(int count);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error discovering workflow context types")]
    partial void LogContextTypeDiscoveryError(ReflectionTypeLoadException ex);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Searching for workflow {WorkflowInstanceId} across {TypeCount} known context types")]
    partial void LogSearchingForWorkflow(string workflowInstanceId, int typeCount);

    [LoggerMessage(
        Level = LogLevel.Trace,
        Message = "Failed to check type {TypeName} for workflow {InstanceId}")]
    partial void LogFailedToCheckType(string typeName, string instanceId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to find ResumeWorkflowAsync method via reflection")]
    partial void LogResumeMethodNotFound();

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Resolved context type for workflow {InstanceId} from metadata: {TypeName}")]
    private partial void LogResolvedContextTypeFromMetadata(string instanceId, string typeName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Could not resolve context type for workflow {InstanceId}")]
    private partial void LogCouldNotResolveContextType(string instanceId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Workflow state {InstanceId} not found")]
    private partial void LogWorkflowStateNotFound(string instanceId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Could not find type information for workflow {InstanceId}")]
    private partial void LogCouldNotFindTypeInfo(string instanceId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Resolved context type for workflow {InstanceId} from known types: {TypeName}")]
    private partial void LogResolvedContextTypeFromKnownTypes(string instanceId, string typeName);

    private sealed record WorkflowStateInfo
    {
        public required string WorkflowInstanceId { get; init; }
        public required WorkflowStatus Status { get; init; }
        public string? WaitingForSignal { get; init; }
    }
}
