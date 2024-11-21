namespace DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

public interface IAsyncValidatable
{
    Task<bool> ValidateAsync();
}
