using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;

namespace DropBear.Codex.Workflow.Persistence.Services;

/// <summary>
/// Background service for processing workflow timeouts
/// </summary>
public sealed class WorkflowTimeoutService : BackgroundService
{
    private readonly IPersistentWorkflowEngine _workflowEngine;
    private readonly IWorkflowStateRepository _stateRepository;
    private readonly ILogger<WorkflowTimeoutService> _logger;

    public WorkflowTimeoutService(
        IPersistentWorkflowEngine workflowEngine,
        IWorkflowStateRepository stateRepository,
        ILogger<WorkflowTimeoutService> logger)
    {
        _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Workflow timeout service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredWorkflows(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Check every 5 minutes
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing workflow timeouts");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait before retrying
            }
        }

        _logger.LogInformation("Workflow timeout service stopped");
    }

    private async Task ProcessExpiredWorkflows(CancellationToken cancellationToken)
    {
        // Get workflows that have exceeded their signal timeout
        var expiredWorkflows = await _stateRepository.GetWaitingWorkflowsAsync<object>(cancellationToken: cancellationToken);
        
        var now = DateTimeOffset.UtcNow;
        
        foreach (var workflow in expiredWorkflows.Where(w => w.SignalTimeoutAt.HasValue && w.SignalTimeoutAt < now))
        {
            _logger.LogWarning("Workflow {WorkflowInstanceId} has timed out waiting for signal {SignalName}", 
                workflow.WorkflowInstanceId, workflow.WaitingForSignal);

            await _workflowEngine.CancelWorkflowAsync(
                workflow.WorkflowInstanceId, 
                $"Timed out waiting for signal: {workflow.WaitingForSignal}", 
                cancellationToken);
        }
    }
}
