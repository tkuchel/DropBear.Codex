#region

using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Extensions;

/// <summary>
///     Extension methods for the ExecutionEngine
/// </summary>
public static class ExecutionEngineExtensions
{
    /// <summary>
    ///     Determines the optimal execution strategy based on task characteristics.
    /// </summary>
    /// <param name="tasks">The collection of tasks to analyze.</param>
    /// <param name="options">Execution options that influence the decision.</param>
    /// <returns>The recommended execution strategy.</returns>
    public static ExecutionStrategy DetermineOptimalExecutionStrategy(
        IReadOnlyDictionary<string, ITask> tasks,
        ExecutionOptions options)
    {
        // If explicitly configured to use parallel or sequential, honor that setting
        if (!options.EnableParallelExecution)
        {
            return ExecutionStrategy.Sequential;
        }

        if (options.StopOnFirstFailure)
        {
            // Sequential execution is required for proper error handling when stopping on first failure
            return ExecutionStrategy.Sequential;
        }

        // No tasks or just one task? Sequential is simpler
        if (tasks.Count <= 1)
        {
            return ExecutionStrategy.Sequential;
        }

        // Analyze the dependency graph to determine if parallel execution would be beneficial

        // 1. Calculate the dependency density (ratio of actual dependencies to maximum possible)
        var totalDependencies = 0;
        var maxPossibleDependencies = tasks.Count * (tasks.Count - 1) / 2; // Maximum edges in a directed graph

        foreach (var task in tasks.Values)
        {
            totalDependencies += task.Dependencies.Count;
        }

        // If the dependency density is high (many tasks depend on each other), sequential is better
        var dependencyDensity = (double)totalDependencies / Math.Max(1, maxPossibleDependencies);
        if (dependencyDensity > 0.5) // Threshold can be tuned
        {
            return ExecutionStrategy.Sequential;
        }

        // 2. Check the maximum width of the dependency graph (max tasks that can run in parallel)
        // If width is low, parallelism won't help much
        var independentTaskCount = tasks.Values.Count(t => t.Dependencies.Count == 0);
        if (independentTaskCount < 2)
        {
            return ExecutionStrategy.Sequential;
        }

        // 3. Check if tasks have significant estimated durations (worth parallelizing)
        var hasTimeIntensiveTasks = tasks.Values.Any(t => t.EstimatedDuration > TimeSpan.FromSeconds(1));

        // Parallel execution makes sense with multiple independent tasks or time-intensive tasks
        return independentTaskCount >= 2 || hasTimeIntensiveTasks
            ? ExecutionStrategy.Parallel
            : ExecutionStrategy.Sequential;
    }
}
