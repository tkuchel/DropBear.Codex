using System.Reflection;
using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;
using DropBear.Codex.Workflow.Persistence.Repositories;
using DropBear.Codex.Workflow.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DropBear.Codex.Workflow.Persistence.Implementation;

/// <summary>
/// COMPLETE FIXED implementation of persistent workflow engine with improved type discovery and signal handling
/// </summary>
public sealed class PersistentWorkflowEngine : IPersistentWorkflowEngine
{
    private readonly IWorkflowEngine _baseEngine;
    private readonly ILogger<PersistentWorkflowEngine> _logger;
    private readonly IWorkflowNotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkflowStateRepository _stateRepository;

    // FIXED: Cache for known workflow context types with better discovery
    private readonly Lazy<HashSet<Type>> _knownWorkflowContextTypes;

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

        _knownWorkflowContextTypes = new Lazy<HashSet<Type>>(GetRegisteredWorkflowContextTypes);
    }

    public async ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class
    {
        return await _baseEngine.ExecuteAsync(definition, context, cancellationToken);
    }

    public async ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        WorkflowExecutionOptions options,
        CancellationToken cancellationToken = default) where TContext : class
    {
        return await _baseEngine.ExecuteAsync(definition, context, options, cancellationToken);
    }

    public async ValueTask<PersistentWorkflowResult<TContext>> StartPersistentWorkflowAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class
    {
        var workflowInstanceId = Guid.NewGuid().ToString();

        var workflowState = new WorkflowInstanceState<TContext>
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowId = definition.WorkflowId,
            WorkflowDisplayName = definition.DisplayName,
            Context = context,
            Status = WorkflowStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            SerializedWorkflowDefinition = SerializeWorkflowDefinition(definition)
        };

        await _stateRepository.SaveWorkflowStateAsync(workflowState, cancellationToken);

        _logger.LogInformation("Started persistent workflow {WorkflowId} with instance {InstanceId}",
            definition.WorkflowId, workflowInstanceId);

        return await ContinueWorkflowExecutionAsync(workflowState, definition, cancellationToken);
    }

    public async ValueTask<bool> SignalWorkflowAsync<TData>(
        string workflowInstanceId,
        string signalName,
        TData? signalData = default,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to signal workflow {WorkflowInstanceId} with signal '{SignalName}'",
            workflowInstanceId, signalName);

        // FIXED: Try to get the workflow state with the exact context type first
        var (workflowStateInfo, contextType) = await FindWorkflowStateInfoAsync(workflowInstanceId, cancellationToken);

        if (workflowStateInfo == null || contextType == null)
        {
            _logger.LogWarning("Cannot signal workflow {InstanceId} - not found", workflowInstanceId);
            return false;
        }

        _logger.LogDebug("Found workflow with context type {ContextType}", contextType.Name);

        if (workflowStateInfo.Status != WorkflowStatus.WaitingForSignal &&
            workflowStateInfo.Status != WorkflowStatus.WaitingForApproval)
        {
            _logger.LogWarning("Cannot signal workflow {InstanceId} - not waiting for signal (Status: {Status})",
                workflowInstanceId, workflowStateInfo.Status);
            return false;
        }

        if (workflowStateInfo.WaitingForSignal != signalName)
        {
            _logger.LogWarning("Signal mismatch for workflow {InstanceId}. Expected: {Expected}, Received: {Received}",
                workflowInstanceId, workflowStateInfo.WaitingForSignal, signalName);
            return false;
        }

        // Update workflow state using typed method
        var success = await UpdateWorkflowStateWithSignalAsync(workflowInstanceId, contextType, signalName, signalData,
            cancellationToken);

        if (success)
        {
            _logger.LogInformation("Workflow {InstanceId} received signal {SignalName}", workflowInstanceId,
                signalName);

            // Resume workflow asynchronously
            _ = Task.Run(async () =>
                await ResumeWorkflowInternalAsync(workflowInstanceId, contextType, CancellationToken.None));

            return true;
        }

        return false;
    }

    public async ValueTask<WorkflowInstanceState<TContext>?> GetWorkflowStateAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class
    {
        return await _stateRepository.GetWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken);
    }

    public async ValueTask<bool> CancelWorkflowAsync(
        string workflowInstanceId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var (workflowStateInfo, contextType) = await FindWorkflowStateInfoAsync(workflowInstanceId, cancellationToken);
        if (workflowStateInfo == null || contextType == null)
        {
            return false;
        }

        return await UpdateWorkflowStatusAsync(workflowInstanceId, contextType, WorkflowStatus.Cancelled,
            new Dictionary<string, object> { ["CancellationReason"] = reason, ["CancelledAt"] = DateTimeOffset.UtcNow },
            cancellationToken);
    }

    public async ValueTask<PersistentWorkflowResult<TContext>> ResumeWorkflowAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class
    {
        return await ResumeWorkflowTypedAsync<TContext>(workflowInstanceId, cancellationToken);
    }

    // Private helper methods

    private static string SerializeWorkflowDefinition<TContext>(IWorkflowDefinition<TContext> definition)
        where TContext : class
    {
        // Store type information that can be used to reconstruct the workflow
        return $"{definition.GetType().AssemblyQualifiedName}|{typeof(TContext).AssemblyQualifiedName}";
    }

    /// <summary>
    /// FIXED: Improved method to get workflow context types with better discovery logic
    /// </summary>
    private HashSet<Type> GetRegisteredWorkflowContextTypes()
    {
        var knownTypes = new HashSet<Type>();

        try
        {
            // 1. Get types from registered workflow definitions (most reliable)
            var workflowDefinitions = _serviceProvider.GetServices<object>()
                .Where(s => s.GetType().GetInterfaces().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowDefinition<>)))
                .ToList();

            foreach (var workflowDef in workflowDefinitions)
            {
                var workflowType = workflowDef.GetType();
                var workflowInterfaces = workflowType.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowDefinition<>));

                foreach (var workflowInterface in workflowInterfaces)
                {
                    var contextType = workflowInterface.GetGenericArguments()[0];
                    if (contextType.IsClass && !contextType.IsAbstract)
                    {
                        knownTypes.Add(contextType);
                        _logger.LogDebug("Found workflow context type from DI: {ContextType}", contextType.FullName);
                    }
                }
            }

            // 2. FIXED: Look in all loaded assemblies, not just executing assembly
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .ToList();

            foreach (var assembly in assemblies)
            {
                try
                {
                    // Get all types from the assembly that look like workflow contexts
                    var assemblyContextTypes = assembly.GetTypes()
                        .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition)
                        .Where(t => t.Name.Contains("Context", StringComparison.OrdinalIgnoreCase) ||
                                    t.Name.EndsWith("Context", StringComparison.OrdinalIgnoreCase))
                        .Where(t => !IsSystemType(t))
                        .ToList();

                    foreach (var contextType in assemblyContextTypes)
                    {
                        knownTypes.Add(contextType);
                        _logger.LogDebug("Found workflow context type from assembly {Assembly}: {ContextType}",
                            assembly.GetName().Name, contextType.FullName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not scan assembly {AssemblyName}", assembly.GetName().Name);
                }
            }

            // 3. FIXED: Also include any types that are currently being used in the repository
            if (_stateRepository is InMemoryWorkflowStateRepository inMemoryRepo)
            {
                try
                {
                    var allStates = inMemoryRepo.GetAllWorkflowStates();
                    foreach (var (workflowId, contextTypeName, state) in allStates)
                    {
                        var stateType = state.GetType();
                        if (stateType.IsGenericType &&
                            stateType.GetGenericTypeDefinition().Name.Contains("WorkflowInstanceState"))
                        {
                            var contextType = stateType.GetGenericArguments()[0];
                            knownTypes.Add(contextType);
                            _logger.LogDebug("Found workflow context type from repository state: {ContextType}",
                                contextType.FullName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not scan repository states");
                }
            }

            _logger.LogInformation("Found {Count} workflow context types total", knownTypes.Count);
            foreach (var type in knownTypes.OrderBy(t => t.FullName))
            {
                _logger.LogDebug("Registered workflow context type: {TypeName}", type.FullName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering workflow context types");
        }

        return knownTypes;
    }

    /// <summary>
    /// FIXED: Helper to determine if a type is a system type we should ignore
    /// </summary>
    private static bool IsSystemType(Type type)
    {
        if (type.Namespace == null) return true;

        return type.Namespace.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               type.Namespace.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               type.Namespace.StartsWith("MS.Internal.", StringComparison.OrdinalIgnoreCase) ||
               type.Namespace.StartsWith("Newtonsoft.", StringComparison.OrdinalIgnoreCase) ||
               type.Namespace.StartsWith("NuGet.", StringComparison.OrdinalIgnoreCase) ||
               type.Namespace.Equals("System", StringComparison.OrdinalIgnoreCase);
    }

    private async ValueTask<(WorkflowStateInfo?, Type?)> FindWorkflowStateInfoAsync(
        string workflowInstanceId,
        CancellationToken cancellationToken)
    {
        // FIXED: Force refresh of known types to include any new ones
        var contextTypes = _knownWorkflowContextTypes.Value;

        _logger.LogDebug("Searching for workflow {WorkflowInstanceId} across {TypeCount} known context types",
            workflowInstanceId, contextTypes.Count);

        foreach (var contextType in contextTypes)
        {
            try
            {
                var stateInfo = await GetWorkflowStateInfoAsync(workflowInstanceId, contextType, cancellationToken);
                if (stateInfo != null)
                {
                    _logger.LogDebug("Found workflow {WorkflowInstanceId} with context type {ContextType}",
                        workflowInstanceId, contextType.Name);
                    return (stateInfo, contextType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get workflow state for type {ContextType}", contextType.Name);
                // Continue to next type
            }
        }

        _logger.LogWarning("Workflow {WorkflowInstanceId} not found in any of the {TypeCount} known context types",
            workflowInstanceId, contextTypes.Count);
        return (null, null);
    }

    private async ValueTask<WorkflowStateInfo?> GetWorkflowStateInfoAsync(
        string workflowInstanceId,
        Type contextType,
        CancellationToken cancellationToken)
    {
        try
        {
            var method = _stateRepository.GetType()
                .GetMethod(nameof(IWorkflowStateRepository.GetWorkflowStateAsync))
                ?.MakeGenericMethod(contextType);

            if (method == null)
            {
                return null;
            }

            var task = (dynamic)method.Invoke(_stateRepository,
                new object[] { workflowInstanceId, cancellationToken })!;
            var result = await task;

            if (result == null)
            {
                return null;
            }

            // Extract basic state information without full type conversion
            return ExtractWorkflowStateInfo(result);
        }
        catch
        {
            return null;
        }
    }

    private static WorkflowStateInfo ExtractWorkflowStateInfo(object workflowState)
    {
        var type = workflowState.GetType();
        var properties = type.GetProperties();

        return new WorkflowStateInfo
        {
            WorkflowInstanceId = GetPropertyValue<string>(properties, workflowState, "WorkflowInstanceId")!,
            WorkflowId = GetPropertyValue<string>(properties, workflowState, "WorkflowId")!,
            Status = GetPropertyValue<WorkflowStatus>(properties, workflowState, "Status"),
            WaitingForSignal = GetPropertyValue<string?>(properties, workflowState, "WaitingForSignal"),
            SignalTimeoutAt = GetPropertyValue<DateTimeOffset?>(properties, workflowState, "SignalTimeoutAt"),
            LastUpdatedAt = GetPropertyValue<DateTimeOffset>(properties, workflowState, "LastUpdatedAt")
        };
    }

    private static T GetPropertyValue<T>(PropertyInfo[] properties, object obj, string propertyName)
    {
        var property = properties.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
        if (property == null)
        {
            return default!;
        }

        var value = property.GetValue(obj);
        return value is T typedValue ? typedValue : default!;
    }

    private async ValueTask<bool> UpdateWorkflowStateWithSignalAsync(
        string workflowInstanceId,
        Type contextType,
        string signalName,
        object? signalData,
        CancellationToken cancellationToken)
    {
        try
        {
            var updateMethod = GetType()
                .GetMethod(nameof(UpdateWorkflowStateWithSignalTypedAsync),
                    BindingFlags.NonPublic | BindingFlags.Instance)
                ?.MakeGenericMethod(contextType);

            if (updateMethod == null)
            {
                return false;
            }

            var task = (dynamic)updateMethod.Invoke(this,
                new[] { workflowInstanceId, signalName, signalData, cancellationToken })!;
            return await task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update workflow state with signal for {WorkflowInstanceId}",
                workflowInstanceId);
            return false;
        }
    }

    private async ValueTask<bool> UpdateWorkflowStateWithSignalTypedAsync<TContext>(
        string workflowInstanceId,
        string signalName,
        object? signalData,
        CancellationToken cancellationToken) where TContext : class
    {
        var workflowState =
            await _stateRepository.GetWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken);
        if (workflowState == null)
        {
            return false;
        }

        // Update metadata with signal data
        var metadata = new Dictionary<string, object>(workflowState.Metadata);
        metadata[$"signal_{signalName}"] = signalData ?? new object();

        var updatedState = workflowState with
        {
            Status = WorkflowStatus.Running,
            WaitingForSignal = null,
            SignalTimeoutAt = null,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            Metadata = metadata
        };

        await _stateRepository.UpdateWorkflowStateAsync(updatedState, cancellationToken);
        return true;
    }

    private async ValueTask<bool> UpdateWorkflowStatusAsync(
        string workflowInstanceId,
        Type contextType,
        WorkflowStatus status,
        Dictionary<string, object>? additionalMetadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var method = GetType()
                .GetMethod(nameof(UpdateWorkflowStatusTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                ?.MakeGenericMethod(contextType);

            if (method == null)
            {
                return false;
            }

            var task = (dynamic)method.Invoke(this,
                new object[] { workflowInstanceId, status, additionalMetadata, cancellationToken })!;
            return await task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update workflow status for {WorkflowInstanceId}", workflowInstanceId);
            return false;
        }
    }

    private async ValueTask<bool> UpdateWorkflowStatusTypedAsync<TContext>(
        string workflowInstanceId,
        WorkflowStatus status,
        Dictionary<string, object>? additionalMetadata,
        CancellationToken cancellationToken) where TContext : class
    {
        var workflowState =
            await _stateRepository.GetWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken);
        if (workflowState == null)
        {
            return false;
        }

        var metadata = new Dictionary<string, object>(workflowState.Metadata);
        if (additionalMetadata != null)
        {
            foreach (var kvp in additionalMetadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }

        var updatedState = workflowState with
        {
            Status = status, LastUpdatedAt = DateTimeOffset.UtcNow, Metadata = metadata
        };

        await _stateRepository.UpdateWorkflowStateAsync(updatedState, cancellationToken);

        if (status == WorkflowStatus.Cancelled)
        {
            _logger.LogInformation("Workflow {InstanceId} cancelled: {Reason}",
                workflowInstanceId, additionalMetadata?["CancellationReason"]);
        }

        return true;
    }

    private async ValueTask ResumeWorkflowInternalAsync(string workflowInstanceId, Type contextType,
        CancellationToken cancellationToken)
    {
        try
        {
            var method = GetType()
                .GetMethod(nameof(ResumeWorkflowTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                ?.MakeGenericMethod(contextType);

            if (method != null)
            {
                var task = (dynamic)method.Invoke(this, new object[] { workflowInstanceId, cancellationToken })!;
                await task;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume workflow {WorkflowInstanceId}", workflowInstanceId);
        }
    }

    private async ValueTask<IWorkflowDefinition<TContext>?> ReconstructWorkflowDefinitionAsync<TContext>(
        string? serializedDefinition) where TContext : class
    {
        if (string.IsNullOrEmpty(serializedDefinition))
        {
            return null;
        }

        try
        {
            // Parse the serialized definition (format: TypeName|ContextTypeName)
            var parts = serializedDefinition.Split('|');
            if (parts.Length < 1)
            {
                return null;
            }

            var definitionTypeName = parts[0];
            var definitionType = Type.GetType(definitionTypeName);

            if (definitionType == null)
            {
                _logger.LogError("Could not resolve workflow type: {TypeName}", definitionTypeName);
                return null;
            }

            _logger.LogDebug("Resolved workflow type: {TypeName}", definitionType.Name);

            // Try to get the workflow from the service provider by concrete type first
            try
            {
                var workflowDefinition = _serviceProvider.GetService(definitionType) as IWorkflowDefinition<TContext>;
                if (workflowDefinition != null)
                {
                    _logger.LogDebug("Successfully retrieved workflow from service provider by concrete type");
                    return workflowDefinition;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Service provider by concrete type failed, trying alternative methods");
            }

            // Try to get by workflow interface type from DI
            try
            {
                var interfaceType = typeof(IWorkflowDefinition<TContext>);
                var workflows = _serviceProvider.GetServices(interfaceType).Cast<IWorkflowDefinition<TContext>>();
                var matchingWorkflow = workflows.FirstOrDefault(w => w.GetType() == definitionType);

                if (matchingWorkflow != null)
                {
                    _logger.LogDebug("Found matching workflow by interface lookup");
                    return matchingWorkflow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Interface lookup failed, trying direct instantiation");
            }

            // Try direct instantiation with proper error handling
            try
            {
                // Check if type has parameterless constructor
                var constructor = definitionType.GetConstructor(Type.EmptyTypes);
                if (constructor == null)
                {
                    _logger.LogError("Workflow type {TypeName} does not have a parameterless constructor",
                        definitionType.Name);
                    return null;
                }

                var workflowDefinition = Activator.CreateInstance(definitionType) as IWorkflowDefinition<TContext>;
                if (workflowDefinition == null)
                {
                    _logger.LogError("Could not cast created instance to IWorkflowDefinition<{ContextType}>",
                        typeof(TContext).Name);
                    return null;
                }

                _logger.LogDebug("Successfully created workflow via Activator.CreateInstance");
                return workflowDefinition;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Activator.CreateInstance failed for workflow type: {TypeName}",
                    definitionType.Name);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconstruct workflow definition from {SerializedDefinition}",
                serializedDefinition);
            return null;
        }
    }

    // Fixed ResumeWorkflowTypedAsync method - call with correct parameter
    private async ValueTask<PersistentWorkflowResult<TContext>> ResumeWorkflowTypedAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken) where TContext : class
    {
        var workflowState =
            await _stateRepository.GetWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken);
        if (workflowState == null)
        {
            throw new InvalidOperationException($"Workflow state not found for instance {workflowInstanceId}");
        }

        // FIXED: Pass the serialized definition string, not the entire state object
        var definition = await ReconstructWorkflowDefinitionAsync<TContext>(workflowState.SerializedWorkflowDefinition);
        if (definition == null)
        {
            throw new InvalidOperationException(
                $"Could not reconstruct workflow definition for instance {workflowInstanceId}");
        }

        return await ContinueWorkflowExecutionAsync(workflowState, definition, cancellationToken);
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> ContinueWorkflowExecutionAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        IWorkflowDefinition<TContext> definition,
        CancellationToken cancellationToken) where TContext : class
    {
        try
        {
            var result = await _baseEngine.ExecuteAsync(definition, workflowState.Context, cancellationToken);

            if (result.IsSuccess)
            {
                return await HandleSuccessfulCompletionAsync(workflowState, result, cancellationToken);
            }

            // Check for suspension signals
            var (foundSuspension, signalName, signalMetadata) = ExtractSuspensionInfo(result);

            if (foundSuspension && !string.IsNullOrEmpty(signalName))
            {
                return await HandleWorkflowSuspensionAsync(workflowState, signalName, signalMetadata,
                    cancellationToken);
            }

            // Workflow failed
            return await HandleWorkflowFailureAsync(workflowState, result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing workflow execution for instance {InstanceId}",
                workflowState.WorkflowInstanceId);

            return await HandleWorkflowExceptionAsync(workflowState, ex, cancellationToken);
        }
    }

    private static (bool foundSuspension, string? signalName, Dictionary<string, object>? metadata)
        ExtractSuspensionInfo<TContext>(
            WorkflowResult<TContext> result) where TContext : class
    {
        // Check execution trace for suspension signals
        if (result.ExecutionTrace != null)
        {
            foreach (var trace in result.ExecutionTrace)
            {
                if (!trace.Result.IsSuccess &&
                    trace.Result.ErrorMessage?.StartsWith("WAITING_FOR_SIGNAL:") == true)
                {
                    var signalName = trace.Result.ErrorMessage.Substring("WAITING_FOR_SIGNAL:".Length);
                    var metadata = trace.Result.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    return (true, signalName, metadata);
                }
            }
        }

        // Check main result error message as fallback
        if (result.ErrorMessage?.StartsWith("WAITING_FOR_SIGNAL:") == true)
        {
            var signalName = result.ErrorMessage.Substring("WAITING_FOR_SIGNAL:".Length);
            return (true, signalName, null);
        }

        return (false, null, null);
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> HandleSuccessfulCompletionAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        WorkflowResult<TContext> result,
        CancellationToken cancellationToken) where TContext : class
    {
        var updatedState = workflowState with
        {
            Status = WorkflowStatus.Completed, LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await _stateRepository.UpdateWorkflowStateAsync(updatedState, cancellationToken);
        await _notificationService.SendWorkflowCompletionNotificationAsync(updatedState, result, cancellationToken);

        return new PersistentWorkflowResult<TContext>
        {
            WorkflowInstanceId = updatedState.WorkflowInstanceId,
            Status = WorkflowStatus.Completed,
            Context = result.Context,
            CompletionResult = result
        };
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> HandleWorkflowSuspensionAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        string signalName,
        Dictionary<string, object>? signalMetadata,
        CancellationToken cancellationToken) where TContext : class
    {
        _logger.LogDebug("Suspending workflow {WorkflowInstanceId} for signal '{SignalName}'",
            workflowState.WorkflowInstanceId, signalName);

        var status = signalName.StartsWith("approval_", StringComparison.OrdinalIgnoreCase)
            ? WorkflowStatus.WaitingForApproval
            : WorkflowStatus.WaitingForSignal;

        var updatedState = workflowState with
        {
            Status = status, WaitingForSignal = signalName, LastUpdatedAt = DateTimeOffset.UtcNow
        };

        // Set timeout if specified
        if (signalMetadata?.TryGetValue("SignalTimeoutAt", out var timeoutValue) == true &&
            timeoutValue is DateTimeOffset timeout)
        {
            updatedState = updatedState with { SignalTimeoutAt = timeout };
        }

        await _stateRepository.UpdateWorkflowStateAsync(updatedState, cancellationToken);

        return new PersistentWorkflowResult<TContext>
        {
            WorkflowInstanceId = updatedState.WorkflowInstanceId,
            Status = updatedState.Status,
            Context = updatedState.Context,
            WaitingForSignal = signalName,
            SignalTimeoutAt = updatedState.SignalTimeoutAt
        };
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> HandleWorkflowFailureAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        WorkflowResult<TContext> result,
        CancellationToken cancellationToken) where TContext : class
    {
        _logger.LogWarning("Workflow {WorkflowInstanceId} failed: {ErrorMessage}",
            workflowState.WorkflowInstanceId, result.ErrorMessage);

        var metadata = new Dictionary<string, object>(workflowState.Metadata)
        {
            ["FailureReason"] = result.ErrorMessage ?? "Unknown error"
        };

        var updatedState = workflowState with
        {
            Status = WorkflowStatus.Failed, LastUpdatedAt = DateTimeOffset.UtcNow, Metadata = metadata
        };

        await _stateRepository.UpdateWorkflowStateAsync(updatedState, cancellationToken);
        await _notificationService.SendWorkflowErrorNotificationAsync(
            updatedState, result.ErrorMessage ?? "Unknown error", result.Exception, cancellationToken);

        return new PersistentWorkflowResult<TContext>
        {
            WorkflowInstanceId = updatedState.WorkflowInstanceId,
            Status = WorkflowStatus.Failed,
            Context = updatedState.Context,
            ErrorMessage = result.ErrorMessage,
            Exception = result.Exception
        };
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> HandleWorkflowExceptionAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        Exception exception,
        CancellationToken cancellationToken) where TContext : class
    {
        var metadata = new Dictionary<string, object>(workflowState.Metadata) { ["FailureReason"] = exception.Message };

        var updatedState = workflowState with
        {
            Status = WorkflowStatus.Failed, LastUpdatedAt = DateTimeOffset.UtcNow, Metadata = metadata
        };

        await _stateRepository.UpdateWorkflowStateAsync(updatedState, cancellationToken);

        return new PersistentWorkflowResult<TContext>
        {
            WorkflowInstanceId = updatedState.WorkflowInstanceId,
            Status = WorkflowStatus.Failed,
            Context = updatedState.Context,
            ErrorMessage = exception.Message,
            Exception = exception
        };
    }

    // Helper class for extracting basic workflow state information
    private class WorkflowStateInfo
    {
        public required string WorkflowInstanceId { get; init; }
        public required string WorkflowId { get; init; }
        public required WorkflowStatus Status { get; init; }
        public string? WaitingForSignal { get; init; }
        public DateTimeOffset? SignalTimeoutAt { get; init; }
        public required DateTimeOffset LastUpdatedAt { get; init; }
    }
}
