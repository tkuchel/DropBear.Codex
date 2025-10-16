#region

using DropBear.Codex.Workflow.Persistence.Configuration;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Services;

/// <summary>
///     Background service for processing workflow timeouts
/// </summary>
public sealed class WorkflowTimeoutService : BackgroundService
{
    private readonly ILogger<WorkflowTimeoutService> _logger;
    private readonly PersistentWorkflowOptions _options;
    private readonly IWorkflowStateRepository _stateRepository;
    private readonly IPersistentWorkflowEngine _workflowEngine;

    /// <summary>
    ///     Initializes a new workflow timeout service.
    /// </summary>
    /// <param name="workflowEngine">Persistent workflow engine</param>
    /// <param name="stateRepository">Workflow state repository</param>
    /// <param name="options">Persistent workflow options</param>
    /// <param name="logger">Logger instance</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null</exception>
    public WorkflowTimeoutService(
        IPersistentWorkflowEngine workflowEngine,
        IWorkflowStateRepository stateRepository,
        IOptions<PersistentWorkflowOptions> options,
        ILogger<WorkflowTimeoutService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Workflow timeout service started");

        if (!_options.EnableTimeoutProcessing)
        {
            _logger.LogInformation("Workflow timeout processing is disabled");
            return;
        }

        TimeSpan checkInterval = _options.TimeoutCheckInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredWorkflowsAsync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(checkInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down
                _logger.LogDebug("Workflow timeout service cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing workflow timeouts");
                // Wait before retrying to avoid tight error loops
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Workflow timeout service stopped");
    }

    private async Task ProcessExpiredWorkflowsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get workflows that have exceeded their signal timeout
            IEnumerable<WorkflowInstanceState<object>> expiredWorkflows =
                await _stateRepository.GetWaitingWorkflowsAsync<object>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            int processedCount = 0;

            foreach (WorkflowInstanceState<object> workflow in expiredWorkflows
                         .Where(w => w.SignalTimeoutAt.HasValue && w.SignalTimeoutAt < now))
            {
                try
                {
                    _logger.LogWarning(
                        "Workflow {WorkflowInstanceId} has timed out waiting for signal {SignalName}",
                        workflow.WorkflowInstanceId,
                        workflow.WaitingForSignal);

                    bool cancelled = await _workflowEngine.CancelWorkflowAsync(
                        workflow.WorkflowInstanceId,
                        $"Timed out waiting for signal: {workflow.WaitingForSignal}",
                        cancellationToken).ConfigureAwait(false);

                    if (cancelled)
                    {
                        processedCount++;
                        _logger.LogInformation(
                            "Successfully cancelled timed-out workflow {WorkflowInstanceId}",
                            workflow.WorkflowInstanceId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to cancel timed-out workflow {WorkflowInstanceId}",
                            workflow.WorkflowInstanceId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error cancelling timed-out workflow {WorkflowInstanceId}",
                        workflow.WorkflowInstanceId);
                }
            }

            if (processedCount > 0)
            {
                _logger.LogInformation(
                    "Processed {Count} timed-out workflows",
                    processedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expired workflows");
            throw;
        }
    }
}
