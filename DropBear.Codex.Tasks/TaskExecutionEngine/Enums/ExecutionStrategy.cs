namespace DropBear.Codex.Tasks.TaskExecutionEngine.Enums;

/// <summary>
///     Represents the execution strategy to use for tasks.
/// </summary>
public enum ExecutionStrategy
{
    /// <summary>
    ///     Let the engine determine the optimal strategy based on task dependencies and characteristics.
    /// </summary>
    Adaptive = 0,

    /// <summary>
    ///     Always execute tasks sequentially in dependency order.
    /// </summary>
    Sequential = 1,

    /// <summary>
    ///     Always execute tasks in parallel, respecting only direct dependencies.
    /// </summary>
    Parallel = 2
}
