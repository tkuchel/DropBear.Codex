using DropBear.Codex.Workflow.Core;
using DropBear.Codex.Workflow.Results;
using DropBear.Codex.Workflow.Persistence.Models;

namespace DropBear.Codex.Workflow.Persistence.Steps;

/// <summary>
/// Workflow step that suspends execution and waits for an external signal
/// </summary>
/// <typeparam name="TContext">The workflow context type</typeparam>
/// <typeparam name="TSignalData">The type of data expected with the signal</typeparam>
public abstract class WaitForSignalStep<TContext, TSignalData> : WorkflowStepBase<TContext> 
    where TContext : class
{
    /// <summary>
    /// The name of the signal to wait for
    /// </summary>
    public abstract string SignalName { get; }

    /// <summary>
    /// Optional timeout for the signal wait
    /// </summary>
    public virtual TimeSpan? SignalTimeout => null;

    /// <summary>
    /// This step always suspends the workflow - actual logic is in ProcessSignal
    /// </summary>
    public override async ValueTask<StepResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Allow for async override

        // Return a special result that indicates we're waiting for a signal
        var metadata = new Dictionary<string, object>
        {
            ["SignalName"] = SignalName,
            ["WaitingStartedAt"] = DateTimeOffset.UtcNow
        };

        if (SignalTimeout.HasValue)
        {
            metadata["SignalTimeoutAt"] = DateTimeOffset.UtcNow.Add(SignalTimeout.Value);
        }

        // This special failure type will be recognized by the persistent engine
        return StepResult.Failure($"WAITING_FOR_SIGNAL:{SignalName}", false, metadata);
    }

    /// <summary>
    /// Called when the signal is received to process the signal data
    /// </summary>
    /// <param name="context">The workflow context</param>
    /// <param name="signalData">The data received with the signal</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of processing the signal</returns>
    public abstract ValueTask<StepResult> ProcessSignalAsync(
        TContext context, 
        TSignalData? signalData, 
        CancellationToken cancellationToken = default);
}
