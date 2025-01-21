namespace DropBear.Codex.Blazor.Interfaces;

public interface IJsInitializationService
{
    Task WaitForJsObjectAsync(string objectName, int maxAttempts = 50);
    Task EnsureJsModuleInitializedAsync(string moduleName, TimeSpan? timeout = null);
}
