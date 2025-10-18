#region

using System.Reflection;
using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Persistence.Interfaces;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Extensions;

public static class WorkflowStateRepositoryExtensions
{
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

                object? result = method.Invoke(repository, new object[] { workflowInstanceId, cancellationToken });

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
