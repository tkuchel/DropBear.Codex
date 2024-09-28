namespace DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

/// <summary>
///     Defines a factory for creating instances of <see cref="ExecutionEngine" />.
/// </summary>
public interface IExecutionEngineFactory
{
    /// <summary>
    ///     Creates a new instance of <see cref="ExecutionEngine" /> with the specified channel ID.
    /// </summary>
    /// <param name="channelId">The channel ID associated with the execution engine.</param>
    /// <returns>A new instance of <see cref="ExecutionEngine" />.</returns>
    ExecutionEngine CreateExecutionEngine(Guid channelId);
}
