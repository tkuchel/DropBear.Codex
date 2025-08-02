using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using DropBear.Codex.Workflow.Core;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;
using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;

namespace DropBear.Codex.Workflow.Implementation;

/// <summary>
/// Fixed implementation of persistent workflow engine that properly handles workflow definition persistence
/// </summary>
public sealed class PersistentWorkflowEngine : IPersistentWorkflowEngine
{
    private readonly IWorkflowEngine _baseEngine;
    private readonly IWorkflowStateRepository _stateRepository;
    private readonly IWorkflowNotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PersistentWorkflowEngine> _logger;

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

    /// <inheritdoc />
    public async ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class
    {
        return await _baseEngine.ExecuteAsync(definition, context, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        WorkflowExecutionOptions options,
        CancellationToken cancellationToken = default) where TContext : class
    {
        return await _baseEngine.ExecuteAsync(definition, context, options, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistentWorkflowResult<TContext>> StartPersistentWorkflowAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class
    {
        var workflowInstanceId = Guid.NewGuid().ToString();

        // Create initial workflow state - STORE THE TYPE NAME instead of serializing the interface
        var workflowState = new WorkflowInstanceState<TContext>
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowId = definition.WorkflowId,
            WorkflowDisplayName = definition.DisplayName,
            Context = context,
            Status = WorkflowStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            SerializedWorkflowDefinition = definition.GetType().AssemblyQualifiedName // Store type name instead
        };

        // Save initial state
        await _stateRepository.SaveWorkflowStateAsync(workflowState, cancellationToken);

        _logger.LogInformation("Started persistent workflow {WorkflowId} with instance {InstanceId}",
            definition.WorkflowId, workflowInstanceId);

        // Try to execute the workflow
        return await ContinueWorkflowExecutionAsync(workflowState, definition, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistentWorkflowResult<TContext>> ResumeWorkflowAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class
    {
        var workflowState = await _stateRepository.GetWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken);
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

        _logger.LogInformation("Resuming workflow instance {InstanceId}", workflowInstanceId);

        // Reconstruct the workflow definition from the service provider
        var definition = await ReconstructWorkflowDefinitionAsync<TContext>(workflowState);

        return await ContinueWorkflowExecutionAsync(workflowState, definition, cancellationToken);
    }

    /// <inheritdoc />
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

        if (workflowState.Status != WorkflowStatus.WaitingForSignal && workflowState.Status != WorkflowStatus.WaitingForApproval)
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

        // Store the signal data in metadata
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

        // Resume workflow execution (this should be done asynchronously in a real implementation)
        _ = Task.Run(async () => await ResumeWorkflowAsync<object>(workflowInstanceId, CancellationToken.None));

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<WorkflowInstanceState<TContext>?> GetWorkflowStateAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class
    {
        return await _stateRepository.GetWorkflowStateAsync<TContext>(workflowInstanceId, cancellationToken);
    }

    /// <inheritdoc />
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

        workflowState = workflowState with
        {
            Status = WorkflowStatus.Cancelled,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };
        workflowState.Metadata["CancellationReason"] = reason;
        workflowState.Metadata["CancelledAt"] = DateTimeOffset.UtcNow;

        await _stateRepository.UpdateWorkflowStateAsync(workflowState, cancellationToken);

        _logger.LogInformation("Workflow {InstanceId} cancelled: {Reason}", workflowInstanceId, reason);
        return true;
    }

    /// <summary>
    /// Reconstructs workflow definition from service provider using the stored type name
    /// </summary>
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
                throw new InvalidOperationException($"Could not resolve workflow type: {workflowState.SerializedWorkflowDefinition}");
            }

            // Try to get the workflow from the service provider
            var workflowDefinition = _serviceProvider.GetService(workflowType) as IWorkflowDefinition<TContext>;
            if (workflowDefinition != null)
            {
                return workflowDefinition;
            }

            // If not registered, try to create an instance
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

    /// <summary>
    /// Continues workflow execution and handles suspension points
    /// </summary>
    private async ValueTask<PersistentWorkflowResult<TContext>> ContinueWorkflowExecutionAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        IWorkflowDefinition<TContext> definition,
        CancellationToken cancellationToken) where TContext : class
    {
        try
        {
            // Execute workflow until completion or suspension
            var result = await _baseEngine.ExecuteAsync(definition, workflowState.Context, cancellationToken);

            if (result.IsSuccess)
            {
                // Workflow completed successfully
                workflowState = workflowState with
                {
                    Status = WorkflowStatus.Completed,
                    LastUpdatedAt = DateTimeOffset.UtcNow
                };
                await _stateRepository.UpdateWorkflowStateAsync(workflowState, cancellationToken);

                // Send completion notification
                await _notificationService.SendWorkflowCompletionNotificationAsync(workflowState, result, cancellationToken);

                return new PersistentWorkflowResult<TContext>
                {
                    WorkflowInstanceId = workflowState.WorkflowInstanceId,
                    Status = WorkflowStatus.Completed,
                    Context = result.Context,
                    CompletionResult = result
                };
            }
            else
            {
                // Check if this is a suspension point (waiting for signal)
                if (result.ErrorMessage?.StartsWith("WAITING_FOR_SIGNAL:") == true)
                {
                    var signalName = result.ErrorMessage.Substring("WAITING_FOR_SIGNAL:".Length);

                    workflowState = workflowState with
                    {
                        Status = signalName.StartsWith("approval_") ? WorkflowStatus.WaitingForApproval : WorkflowStatus.WaitingForSignal,
                        WaitingForSignal = signalName,
                        LastUpdatedAt = DateTimeOffset.UtcNow
                    };

                    // Set timeout if specified
                    if (result.ExecutionTrace?.LastOrDefault()?.Result.Metadata?.ContainsKey("SignalTimeoutAt") == true)
                    {
                        workflowState = workflowState with
                        {
                            SignalTimeoutAt = (DateTimeOffset)result.ExecutionTrace.Last().Result.Metadata["SignalTimeoutAt"]
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
                else
                {
                    // Workflow failed
                    workflowState = workflowState with
                    {
                        Status = WorkflowStatus.Failed,
                        LastUpdatedAt = DateTimeOffset.UtcNow
                    };
                    workflowState.Metadata["FailureReason"] = result.ErrorMessage ?? "Unknown error";

                    await _stateRepository.UpdateWorkflowStateAsync(workflowState, cancellationToken);

                    // Send error notification
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
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing workflow execution for instance {InstanceId}", workflowState.WorkflowInstanceId);

            workflowState = workflowState with
            {
                Status = WorkflowStatus.Failed,
                LastUpdatedAt = DateTimeOffset.UtcNow
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
