namespace DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

/// <summary>
///     Defines a contract for tasks or objects that support asynchronous validation.
/// </summary>
public interface IAsyncValidatable
{
    /// <summary>
    ///     Validates the object asynchronously, returning <c>true</c> if valid.
    /// </summary>
    Task<bool> ValidateAsync();
}
