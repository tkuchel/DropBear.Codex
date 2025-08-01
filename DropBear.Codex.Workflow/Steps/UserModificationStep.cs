using DropBear.Codex.Workflow.Results;
using DropBear.Codex.Workflow.Persistence.Models;

namespace DropBear.Codex.Workflow.Persistence.Steps;

/// <summary>
/// Workflow step that allows user to modify data and stores pending changes
/// </summary>
/// <typeparam name="TContext">The workflow context type</typeparam>
/// <typeparam name="TModificationData">The type of modification data</typeparam>
public abstract class UserModificationStep<TContext, TModificationData> : WaitForSignalStep<TContext, TModificationData>
    where TContext : class
    where TModificationData : class
{
    public override string SignalName => $"user_modification_{GetType().Name}_{GetHashCode()}";
    public override TimeSpan? SignalTimeout => TimeSpan.FromHours(24); // Default 24-hour timeout

    /// <summary>
    /// Prepares the data that the user can modify
    /// </summary>
    /// <param name="context">The workflow context</param>
    /// <returns>The data to be modified</returns>
    public abstract ValueTask<TModificationData> PrepareModificationDataAsync(TContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and applies the user's modifications
    /// </summary>
    public override async ValueTask<StepResult> ProcessSignalAsync(
        TContext context, 
        TModificationData? modificationData, 
        CancellationToken cancellationToken = default)
    {
        if (modificationData == null)
        {
            return Failure("No modification data received");
        }

        // Validate the modifications
        var validationResult = await ValidateModificationsAsync(context, modificationData, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Failure($"Modification validation failed: {validationResult.ErrorMessage}");
        }

        // Store pending changes without committing them yet
        await StorePendingChangesAsync(context, modificationData, cancellationToken);

        var metadata = new Dictionary<string, object>
        {
            ["ModificationsStored"] = true,
            ["ModifiedAt"] = DateTimeOffset.UtcNow,
            ["HasPendingChanges"] = true
        };

        return Success(metadata);
    }

    /// <summary>
    /// Validates the user's modifications
    /// </summary>
    protected abstract ValueTask<ValidationResult> ValidateModificationsAsync(
        TContext context, 
        TModificationData modificationData, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the pending changes without committing them
    /// </summary>
    protected abstract ValueTask StorePendingChangesAsync(
        TContext context, 
        TModificationData modificationData, 
        CancellationToken cancellationToken = default);
}
