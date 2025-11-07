#region

using System.Reflection;
using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Persistence.Interfaces;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Extensions;

/// <summary>
///     Provides extension methods for <see cref="IWorkflowStateRepository"/> to support
///     type discovery and workflow state retrieval without knowing the exact context type at compile time.
/// </summary>
public static class WorkflowStateRepositoryExtensions
{
    /// <summary>
    ///     Attempts to retrieve workflow state information by dynamically discovering the correct context type
    ///     from a collection of candidate types using reflection.
    /// </summary>
    /// <param name="repository">The workflow state repository to query.</param>
    /// <param name="workflowInstanceId">The unique identifier of the workflow instance to retrieve.</param>
    /// <param name="candidateTypes">
    ///     A collection of possible context types to try. The method will iterate through these types
    ///     and attempt to deserialize the workflow state using each type until successful.
    /// </param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A tuple containing the workflow status, waiting signal (if any), and the discovered context type
    ///     if a matching state was found; otherwise, null if no matching state exists for any candidate type.
    /// </returns>
    /// <remarks>
    ///     This method uses reflection to dynamically invoke the generic <see cref="IWorkflowStateRepository.GetWorkflowStateAsync{TContext}"/>
    ///     method for each candidate type. It's particularly useful when the exact context type is not known at compile time,
    ///     such as when resuming workflows from persistent storage or handling polymorphic workflow contexts.
    ///     <para>
    ///     Performance consideration: This method uses reflection and should not be used in performance-critical hot paths.
    ///     Consider caching discovered types if repeatedly querying the same workflow instance.
    ///     </para>
    /// </remarks>
    public static async ValueTask<(WorkflowStatus Status, string? WaitingForSignal, Type? ContextType)?>
        TryGetWorkflowStateInfoAsync(
            this IWorkflowStateRepository repository,
            string workflowInstanceId,
            IEnumerable<Type> candidateTypes,
            CancellationToken cancellationToken = default)
    {
        foreach (Type type in candidateTypes)
        {
            try
            {
                // Use reflection to call the generic method
                MethodInfo method = typeof(IWorkflowStateRepository)
                    .GetMethod(nameof(IWorkflowStateRepository.GetWorkflowStateAsync))!
                    .MakeGenericMethod(type);

                object? result = method.Invoke(repository, [workflowInstanceId, cancellationToken]);

                if (result is null)
                {
                    continue;
                }

                // Convert ValueTask to Task
                var asTask = result.GetType().GetMethod("AsTask")!.Invoke(result, null) as Task;
                await asTask!.ConfigureAwait(false);

                object? state = asTask.GetType().GetProperty("Result")!.GetValue(asTask);

                if (state is null)
                {
                    continue;
                }

                // Found the right type!
                var status = (WorkflowStatus)state.GetType().GetProperty("Status")!.GetValue(state)!;
                string? signal = state.GetType().GetProperty("WaitingForSignal")!.GetValue(state) as string;

                return (status, signal, type);
            }
            catch
            {
                // Wrong type, continue searching
            }
        }

        return null;
    }
}
