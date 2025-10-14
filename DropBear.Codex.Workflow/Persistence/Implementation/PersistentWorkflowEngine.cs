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
using Serilog;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Implementation;

/// <summary>
///     Persistent workflow engine with improved type discovery and signal handling.
/// </summary>
public sealed class PersistentWorkflowEngine : IPersistentWorkflowEngine
{
    private readonly IWorkflowEngine _baseEngine;
    private readonly Lazy<HashSet<Type>> _knownWorkflowContextTypes;
    private readonly ILogger _logger;
    private readonly IWorkflowNotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkflowStateRepository _stateRepository;

    public PersistentWorkflowEngine(
        IWorkflowEngine baseEngine,
        IWorkflowStateRepository stateRepository,
        IWorkflowNotificationService notificationService,
        IServiceProvider serviceProvider,
        ILogger logger)
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
        CancellationToken cancellationToken = default) where TContext : class =>
        await _baseEngine.ExecuteAsync(definition, context, cancellationToken);

    public async ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        WorkflowExecutionOptions options,
        CancellationToken cancellationToken = default) where TContext : class =>
        await _baseEngine.ExecuteAsync(definition, context, options, cancellationToken);

    public async ValueTask<PersistentWorkflowResult<TContext>> StartPersistentWorkflowAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class
    {
        string workflowInstanceId = Guid.NewGuid().ToString();

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

        _logger.Information(
            "Started persistent workflow {WorkflowId} with instance {InstanceId}",
            definition.WorkflowId,
            workflowInstanceId);

        return await ContinueWorkflowExecutionAsync(workflowState, definition, cancellationToken);
    }

    public async ValueTask<PersistentWorkflowResult<TContext>> ResumeWorkflowAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class
    {
        WorkflowInstanceState<TContext>? workflowState =
            await _stateRepository.GetWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken);

        if (workflowState is null)
        {
            _logger.Warning("Workflow instance {InstanceId} not found", workflowInstanceId);
            throw new InvalidOperationException($"Workflow instance {workflowInstanceId} not found");
        }

        IWorkflowDefinition<TContext>? definition =
            DeserializeWorkflowDefinition<TContext>(workflowState.SerializedWorkflowDefinition);
        if (definition is null)
        {
            throw new InvalidOperationException($"Failed to deserialize workflow definition for {workflowInstanceId}");
        }

        return await ContinueWorkflowExecutionAsync(workflowState, definition, cancellationToken);
    }

    public async ValueTask<bool> SignalWorkflowAsync<TData>(
        string workflowInstanceId,
        string signalName,
        TData? signalData = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        (WorkflowStateInfo? stateInfo, Type? contextType) =
            await FindWorkflowStateInfoAsync(workflowInstanceId, cancellationToken);

        if (stateInfo is null || contextType is null)
        {
            _logger.Warning("Workflow instance {InstanceId} not found for signaling", workflowInstanceId);
            return false;
        }

        if (stateInfo.WaitingForSignal != signalName)
        {
            _logger.Warning(
                "Workflow {InstanceId} is waiting for signal '{ExpectedSignal}', but received '{ReceivedSignal}'",
                workflowInstanceId,
                stateInfo.WaitingForSignal,
                signalName);
            return false;
        }

        MethodInfo? resumeMethod = typeof(PersistentWorkflowEngine)
            .GetMethod(nameof(ResumeWorkflowAsync), BindingFlags.Public | BindingFlags.Instance)
            ?.MakeGenericMethod(contextType);

        if (resumeMethod is null)
        {
            _logger.Error("Failed to get ResumeWorkflowAsync method for type {ContextType}", contextType.Name);
            return false;
        }

        try
        {
            var resumeTask =
                (ValueTask<object>)resumeMethod.Invoke(this, new object[] { workflowInstanceId, cancellationToken })!;
            await resumeTask;

            _logger.Information(
                "Successfully signaled and resumed workflow {InstanceId} with signal {SignalName}",
                workflowInstanceId,
                signalName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to resume workflow {InstanceId} after signal {SignalName}", workflowInstanceId,
                signalName);
            return false;
        }
    }

    public async ValueTask<WorkflowInstanceState<TContext>?> GetWorkflowStateAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class =>
        await _stateRepository.GetWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken);

    public async ValueTask<bool> CancelWorkflowAsync(
        string workflowInstanceId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        (WorkflowStateInfo? stateInfo, Type? contextType) =
            await FindWorkflowStateInfoAsync(workflowInstanceId, cancellationToken);

        if (stateInfo is null || contextType is null)
        {
            _logger.Warning("Workflow instance {InstanceId} not found for cancellation", workflowInstanceId);
            return false;
        }

        MethodInfo? getStateMethod = _stateRepository.GetType()
            .GetMethod(nameof(IWorkflowStateRepository.GetWorkflowStateAsync))
            ?.MakeGenericMethod(contextType);

        if (getStateMethod is null)
        {
            return false;
        }

        dynamic? stateTask =
            getStateMethod.Invoke(_stateRepository, new object[] { workflowInstanceId, cancellationToken });
        if (stateTask is null)
        {
            return false;
        }

        dynamic? workflowState = await stateTask;
        if (workflowState is null)
        {
            return false;
        }

        workflowState.Status = WorkflowStatus.Cancelled;
        workflowState.LastUpdatedAt = DateTimeOffset.UtcNow;
        workflowState.Metadata["CancellationReason"] = reason;

        MethodInfo? updateStateMethod = _stateRepository.GetType()
            .GetMethod(nameof(IWorkflowStateRepository.UpdateWorkflowStateAsync))
            ?.MakeGenericMethod(contextType);

        if (updateStateMethod is not null)
        {
            await (dynamic)updateStateMethod.Invoke(_stateRepository, new object[] { workflowState, cancellationToken })
                !;
        }

        _logger.Information("Cancelled workflow {InstanceId}: {Reason}", workflowInstanceId, reason);
        return true;
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> ContinueWorkflowExecutionAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        IWorkflowDefinition<TContext> definition,
        CancellationToken cancellationToken) where TContext : class
    {
        try
        {
            WorkflowResult<TContext> result =
                await _baseEngine.ExecuteAsync(definition, workflowState.Context, cancellationToken);

            if (result.IsSuspended)
            {
                return await HandleWorkflowSuspensionAsync(workflowState, result, cancellationToken);
            }

            if (result.IsSuccess)
            {
                return await HandleWorkflowCompletionAsync(workflowState, result, cancellationToken);
            }

            return await HandleWorkflowFailureAsync(workflowState, result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error executing persistent workflow {InstanceId}",
                workflowState.WorkflowInstanceId);
            return await HandleWorkflowExceptionAsync(workflowState, ex, cancellationToken);
        }
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> HandleWorkflowSuspensionAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        WorkflowResult<TContext> result,
        CancellationToken cancellationToken) where TContext : class
    {
        WorkflowInstanceState<TContext> updatedState = workflowState with
        {
            Status = WorkflowStatus.WaitingForSignal,
            Context = result.Context,
            WaitingForSignal = result.SuspendedSignalName,
            SignalTimeoutAt = DateTimeOffset.UtcNow.Add(WorkflowConstants.Defaults.DefaultSignalTimeout),
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await _stateRepository.UpdateWorkflowStateAsync(updatedState, cancellationToken);

        return new PersistentWorkflowResult<TContext>
        {
            WorkflowInstanceId = updatedState.WorkflowInstanceId,
            Status = WorkflowStatus.WaitingForSignal,
            Context = updatedState.Context,
            WaitingForSignal = result.SuspendedSignalName,
            SignalTimeoutAt = updatedState.SignalTimeoutAt
        };
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> HandleWorkflowCompletionAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        WorkflowResult<TContext> result,
        CancellationToken cancellationToken) where TContext : class
    {
        WorkflowInstanceState<TContext> updatedState = workflowState with
        {
            Status = WorkflowStatus.Completed, Context = result.Context, LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await _stateRepository.UpdateWorkflowStateAsync(updatedState, cancellationToken);
        await _notificationService.SendWorkflowCompletionNotificationAsync(updatedState, result, cancellationToken);

        return new PersistentWorkflowResult<TContext>
        {
            WorkflowInstanceId = updatedState.WorkflowInstanceId,
            Status = WorkflowStatus.Completed,
            Context = updatedState.Context,
            CompletionResult = result
        };
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> HandleWorkflowFailureAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        WorkflowResult<TContext> result,
        CancellationToken cancellationToken) where TContext : class
    {
        var metadata = new Dictionary<string, object>(workflowState.Metadata)
        {
            ["FailureReason"] = result.ErrorMessage ?? "Unknown error"
        };

        WorkflowInstanceState<TContext> updatedState = workflowState with
        {
            Status = WorkflowStatus.Failed, LastUpdatedAt = DateTimeOffset.UtcNow, Metadata = metadata
        };

        await _stateRepository.UpdateWorkflowStateAsync(updatedState, cancellationToken);
        await _notificationService.SendWorkflowErrorNotificationAsync(
            updatedState,
            result.ErrorMessage ?? "Unknown error",
            result.SourceException,
            cancellationToken);

        return new PersistentWorkflowResult<TContext>
        {
            WorkflowInstanceId = updatedState.WorkflowInstanceId,
            Status = WorkflowStatus.Failed,
            Context = updatedState.Context,
            ErrorMessage = result.ErrorMessage,
            Exception = result.SourceException
        };
    }

    private async ValueTask<PersistentWorkflowResult<TContext>> HandleWorkflowExceptionAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        Exception exception,
        CancellationToken cancellationToken) where TContext : class
    {
        var metadata = new Dictionary<string, object>(workflowState.Metadata) { ["FailureReason"] = exception.Message };

        WorkflowInstanceState<TContext> updatedState = workflowState with
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

    private HashSet<Type> GetRegisteredWorkflowContextTypes()
    {
        var contextTypes = new HashSet<Type>();

        try
        {
            IEnumerable<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !IsSystemAssembly(a));

            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    IEnumerable<Type> types = assembly.GetTypes()
                        .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType);

                    foreach (Type type in types)
                    {
                        if (IsWorkflowContextType(type))
                        {
                            contextTypes.Add(type);
                            _logger.Debug("Discovered workflow context type: {TypeName}", type.FullName);
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    _logger.Debug(ex, "Failed to load types from assembly {AssemblyName}", assembly.FullName);
                }
            }

            _logger.Information("Discovered {Count} workflow context types", contextTypes.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error discovering workflow context types");
        }

        return contextTypes;
    }

    private static bool IsWorkflowContextType(Type type)
    {
        if (!type.IsClass || type.IsAbstract || type.IsGenericType)
        {
            return false;
        }

        if (type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
        {
            return false;
        }

        if (type.Namespace?.StartsWith("Microsoft", StringComparison.Ordinal) == true)
        {
            return false;
        }

        return type.GetProperties().Length > 0;
    }

    private static bool IsSystemAssembly(Assembly assembly)
    {
        string assemblyName = assembly.GetName().Name ?? string.Empty;
        return assemblyName.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase);
    }

    private async ValueTask<(WorkflowStateInfo?, Type?)> FindWorkflowStateInfoAsync(
        string workflowInstanceId,
        CancellationToken cancellationToken)
    {
        HashSet<Type> contextTypes = _knownWorkflowContextTypes.Value;

        _logger.Debug(
            "Searching for workflow {WorkflowInstanceId} across {TypeCount} known context types",
            workflowInstanceId,
            contextTypes.Count);

        foreach (Type contextType in contextTypes)
        {
            try
            {
                WorkflowStateInfo? stateInfo =
                    await GetWorkflowStateInfoAsync(workflowInstanceId, contextType, cancellationToken);
                if (stateInfo != null)
                {
                    _logger.Debug(
                        "Found workflow {WorkflowInstanceId} with context type {ContextType}",
                        workflowInstanceId,
                        contextType.Name);
                    return (stateInfo, contextType);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to get workflow state for type {ContextType}", contextType.Name);
            }
        }

        _logger.Warning(
            "Workflow {WorkflowInstanceId} not found in any of the {TypeCount} known context types",
            workflowInstanceId,
            contextTypes.Count);
        return (null, null);
    }

    private async ValueTask<WorkflowStateInfo?> GetWorkflowStateInfoAsync(
        string workflowInstanceId,
        Type contextType,
        CancellationToken cancellationToken)
    {
        try
        {
            MethodInfo? method = _stateRepository.GetType()
                .GetMethod(nameof(IWorkflowStateRepository.GetWorkflowStateAsync))
                ?.MakeGenericMethod(contextType);

            if (method is null)
            {
                return null;
            }

            dynamic? task = method.Invoke(_stateRepository, new object[] { workflowInstanceId, cancellationToken });
            if (task is null)
            {
                return null;
            }

            dynamic? state = await task;
            if (state is null)
            {
                return null;
            }

            return new WorkflowStateInfo
            {
                WorkflowInstanceId = state.WorkflowInstanceId,
                WorkflowId = state.WorkflowId,
                Status = state.Status,
                WaitingForSignal = state.WaitingForSignal,
                SignalTimeoutAt = state.SignalTimeoutAt,
                LastUpdatedAt = state.LastUpdatedAt
            };
        }
        catch
        {
            return null;
        }
    }

    private static string SerializeWorkflowDefinition<TContext>(IWorkflowDefinition<TContext> definition)
        where TContext : class
    {
        var metadata = new
        {
            definition.WorkflowId,
            definition.DisplayName,
            Version = definition.Version.ToString(),
            definition.WorkflowTimeout
        };

        return JsonSerializer.Serialize(metadata);
    }

    private IWorkflowDefinition<TContext>? DeserializeWorkflowDefinition<TContext>(string? serialized)
        where TContext : class
    {
        if (string.IsNullOrEmpty(serialized))
        {
            return null;
        }

        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            IWorkflowDefinition<TContext>? definition =
                scope.ServiceProvider.GetService<IWorkflowDefinition<TContext>>();
            return definition;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to deserialize workflow definition");
            return null;
        }
    }

    private sealed class WorkflowStateInfo
    {
        public required string WorkflowInstanceId { get; init; }
        public required string WorkflowId { get; init; }
        public required WorkflowStatus Status { get; init; }
        public string? WaitingForSignal { get; init; }
        public DateTimeOffset? SignalTimeoutAt { get; init; }
        public required DateTimeOffset LastUpdatedAt { get; init; }
    }
}
