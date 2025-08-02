#region

using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;
using DropBear.Codex.Workflow.Results;
using Microsoft.Extensions.Logging;

#endregion

/// <summary>
///     Fixed implementation of persistent workflow engine that properly handles workflow definition persistence
/// </summary>
public sealed class PersistentWorkflowEngine : IPersistentWorkflowEngine
{
    private readonly IWorkflowEngine _baseEngine;
    private readonly ILogger<PersistentWorkflowEngine> _logger;
    private readonly IWorkflowNotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkflowStateRepository _stateRepository;

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

        // Store the TYPE NAME instead of trying to serialize the interface
        var workflowState = new WorkflowInstanceState<TContext>
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowId = definition.WorkflowId,
            WorkflowDisplayName = definition.DisplayName,
            Context = context,
            Status = WorkflowStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            SerializedWorkflowDefinition = definition.GetType().AssemblyQualifiedName // Store type name
        };

        await _stateRepository.SaveWorkflowStateAsync(workflowState, cancellationToken);

        _logger.LogInformation("Started persistent workflow {WorkflowId} with instance {InstanceId}",
            definition.WorkflowId, workflowInstanceId);

        return await ContinueWorkflowExecutionAsync(workflowState, definition, cancellationToken);
    }

    public async ValueTask<PersistentWorkflowResult<TContext>> ResumeWorkflowAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class
    {
        var workflowState =
            await _stateRepository.GetWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken);
        if (workflowState == null)
        {
            throw new InvalidOperationException($"Workflow instance {workflowInstanceId} not found");
        }

        if (workflowState.Status == WorkflowStatus.Completed)
        {
            return new PersistentWorkflowResult<TContext>
            {
                WorkflowInstanceId = workflowInstanceId,
                Status = WorkflowStatus.Completed,
                Context = workflowState.Context
            };
        }

        // Reconstruct workflow definition from service provider
        var definition = await ReconstructWorkflowDefinitionAsync(workflowState);

        return await ContinueWorkflowExecutionAsync(workflowState, definition, cancellationToken);
    }

    public async ValueTask<bool> SignalWorkflowAsync<TData>(
        string workflowInstanceId,
        string signalName,
        TData? signalData = default,
        CancellationToken cancellationToken = default)
    {
        var workflowState = await _stateRepository.GetWorkflowStateAsync<object>(workflowInstanceId, cancellationToken);
        if (workflowState == null)
        {
            _logger.LogWarning("Cannot signal workflow {InstanceId} - not found", workflowInstanceId);
            return false;
        }

        if (workflowState.Status != WorkflowStatus.WaitingForSignal &&
            workflowState.Status != WorkflowStatus.WaitingForApproval)
        {
            _logger.LogWarning("Cannot signal workflow {InstanceId} - not waiting for signal (Status: {Status})",
                workflowInstanceId, workflowState.Status);
            return false;
        }

        if (workflowState.WaitingForSignal != signalName)
        {
            _logger.LogWarning("Signal mismatch for workflow {InstanceId}. Expected: {Expected}, Received: {Received}",
                workflowInstanceId, workflowState.WaitingForSignal, signalName);
            return false;
        }

        // Store signal data and update state
        workflowState.Metadata[$"signal_{signalName}"] = signalData ?? new object();
        workflowState = workflowState with
        {
            Status = WorkflowStatus.Running,
            WaitingForSignal = null,
            SignalTimeoutAt = null,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await _stateRepository.UpdateWorkflowStateAsync(workflowState, cancellationToken);

        _logger.LogInformation("Workflow {InstanceId} received signal {SignalName}", workflowInstanceId, signalName);

        // Resume execution asynchronously
        _ = Task.Run(async () => await ResumeWorkflowAsync<object>(workflowInstanceId, CancellationToken.None));

        return true;
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
        var workflowState = await _stateRepository.GetWorkflowStateAsync<object>(workflowInstanceId, cancellationToken);
        if (workflowState == null)
        {
            return false;
        }

        workflowState = workflowState with { Status = WorkflowStatus.Cancelled, LastUpdatedAt = DateTimeOffset.UtcNow };
        workflowState.Metadata["CancellationReason"] = reason;
        workflowState.Metadata["CancelledAt"] = DateTimeOffset.UtcNow;

        await _stateRepository.UpdateWorkflowStateAsync(workflowState, cancellationToken);

        _logger.LogInformation("Workflow {InstanceId} cancelled: {Reason}", workflowInstanceId, reason);
        return true;
    }

    private async ValueTask<IWorkflowDefinition<TContext>> ReconstructWorkflowDefinitionAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState) where TContext : class
    {
        try
        {
            if (string.IsNullOrEmpty(workflowState.SerializedWorkflowDefinition))
            {
                throw new InvalidOperationException("No workflow definition type stored");
            }

            // Get the type from the assembly qualified name
            var workflowType = Type.GetType(workflowState.SerializedWorkflowDefinition);
            if (workflowType == null)
            {
                throw new InvalidOperationException(
                    $"Could not resolve workflow type: {workflowState.SerializedWorkflowDefinition}");
            }

            // Try to get from service provider first
            var workflowDefinition = _serviceProvider.GetService(workflowType) as IWorkflowDefinition<TContext>;
            if (workflowDefinition != null)
            {
                return workflowDefinition;
            }

            // If not registered, create instance directly
            workflowDefinition = Activator.CreateInstance(workflowType) as IWorkflowDefinition<TContext>;
            if (workflowDefinition == null)
            {
                throw new InvalidOperationException($"Could not create instance of workflow type: {workflowType.Name}");
            }

            return workflowDefinition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconstruct workflow definition for instance {InstanceId}",
                workflowState.WorkflowInstanceId);
            throw;
        }
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
                // Completed successfully
                workflowState = workflowState with
                {
                    Status = WorkflowStatus.Completed, LastUpdatedAt = DateTimeOffset.UtcNow
                };
                await _stateRepository.UpdateWorkflowStateAsync(workflowState, cancellationToken);

                await _notificationService.SendWorkflowCompletionNotificationAsync(workflowState, result,
                    cancellationToken);

                return new PersistentWorkflowResult<TContext>
                {
                    WorkflowInstanceId = workflowState.WorkflowInstanceId,
                    Status = WorkflowStatus.Completed,
                    Context = result.Context,
                    CompletionResult = result
                };
            }

            // FIXED: Check for signal waiting by examining execution trace for suspension signals
            var foundSuspensionSignal = false;
            string? signalName = null;
            Dictionary<string, object>? signalMetadata = null;

            // Check the execution trace for any step that returned a suspension signal
            if (result.ExecutionTrace != null)
            {
                foreach (var trace in result.ExecutionTrace)
                {
                    if (!trace.Result.IsSuccess &&
                        trace.Result.ErrorMessage?.StartsWith("WAITING_FOR_SIGNAL:") == true)
                    {
                        foundSuspensionSignal = true;
                        signalName = trace.Result.ErrorMessage.Substring("WAITING_FOR_SIGNAL:".Length);
                        signalMetadata = trace.Result.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                        Console.WriteLine($"  🔍 FIXED ENGINE: Found suspension signal '{signalName}' in trace");
                        break;
                    }
                }
            }

            // ALSO check the main result error message as fallback
            if (!foundSuspensionSignal &&
                result.ErrorMessage?.StartsWith("WAITING_FOR_SIGNAL:") == true)
            {
                foundSuspensionSignal = true;
                signalName = result.ErrorMessage.Substring("WAITING_FOR_SIGNAL:".Length);
                Console.WriteLine($"  🔍 FIXED ENGINE: Found suspension signal '{signalName}' in main result");
            }

            if (foundSuspensionSignal && !string.IsNullOrEmpty(signalName))
            {
                Console.WriteLine($"  🔍 FIXED ENGINE: Suspending workflow for signal '{signalName}'");

                workflowState = workflowState with
                {
                    Status = signalName.StartsWith("approval_")
                        ? WorkflowStatus.WaitingForApproval
                        : WorkflowStatus.WaitingForSignal,
                    WaitingForSignal = signalName,
                    LastUpdatedAt = DateTimeOffset.UtcNow
                };

                // Set timeout if specified in metadata
                if (signalMetadata?.ContainsKey("SignalTimeoutAt") == true)
                {
                    workflowState = workflowState with
                    {
                        SignalTimeoutAt = (DateTimeOffset)signalMetadata["SignalTimeoutAt"]
                    };
                }

                await _stateRepository.UpdateWorkflowStateAsync(workflowState, cancellationToken);

                return new PersistentWorkflowResult<TContext>
                {
                    WorkflowInstanceId = workflowState.WorkflowInstanceId,
                    Status = workflowState.Status,
                    Context = workflowState.Context,
                    WaitingForSignal = signalName,
                    SignalTimeoutAt = workflowState.SignalTimeoutAt
                };
            }

            // Workflow failed (no suspension signal found)
            Console.WriteLine("  🔍 FIXED ENGINE: No suspension signal found, treating as failure");
            Console.WriteLine($"  🔍 FIXED ENGINE: Error message was: '{result.ErrorMessage}'");

            workflowState = workflowState with
            {
                Status = WorkflowStatus.Failed, LastUpdatedAt = DateTimeOffset.UtcNow
            };
            workflowState.Metadata["FailureReason"] = result.ErrorMessage ?? "Unknown error";

            await _stateRepository.UpdateWorkflowStateAsync(workflowState, cancellationToken);

            await _notificationService.SendWorkflowErrorNotificationAsync(
                workflowState, result.ErrorMessage ?? "Unknown error", result.Exception, cancellationToken);

            return new PersistentWorkflowResult<TContext>
            {
                WorkflowInstanceId = workflowState.WorkflowInstanceId,
                Status = WorkflowStatus.Failed,
                Context = workflowState.Context,
                ErrorMessage = result.ErrorMessage,
                Exception = result.Exception
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing workflow execution for instance {InstanceId}",
                workflowState.WorkflowInstanceId);

            workflowState = workflowState with
            {
                Status = WorkflowStatus.Failed, LastUpdatedAt = DateTimeOffset.UtcNow
            };
            workflowState.Metadata["FailureReason"] = ex.Message;

            await _stateRepository.UpdateWorkflowStateAsync(workflowState, cancellationToken);

            return new PersistentWorkflowResult<TContext>
            {
                WorkflowInstanceId = workflowState.WorkflowInstanceId,
                Status = WorkflowStatus.Failed,
                Context = workflowState.Context,
                ErrorMessage = ex.Message,
                Exception = ex
            };
        }
    }
}
