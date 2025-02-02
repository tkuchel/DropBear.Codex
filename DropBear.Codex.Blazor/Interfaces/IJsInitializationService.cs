namespace DropBear.Codex.Blazor.Interfaces;

public interface IJsInitializationService
{
    Task EnsureJsModuleInitializedAsync(string moduleName, TimeSpan? timeout = null);
}
