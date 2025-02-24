namespace DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

/// <summary>
///     Indicates an object that can be reset to an initial or default state.
/// </summary>
public interface IResettable
{
    /// <summary>
    ///     Resets the object to its default state.
    /// </summary>
    void Reset();
}
