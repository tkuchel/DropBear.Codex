using Microsoft.JSInterop;

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Service for initializing JavaScript modules in Blazor components.
///     Enhanced for .NET 9 with improved async patterns.
/// </summary>
public interface IJsInitializationService
{
    /// <summary>
    ///     Ensures a JavaScript module is properly initialized.
    /// </summary>
    /// <param name="moduleName">The name of the module to initialize.</param>
    /// <param name="timeout">Optional timeout for the initialization.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A ValueTask representing the initialization operation.</returns>
    ValueTask EnsureJsModuleInitializedAsync(
        string moduleName,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a JavaScript module is already initialized.
    /// </summary>
    /// <param name="moduleName">The name of the module to check.</param>
    /// <returns>True if the module is initialized, false otherwise.</returns>
    bool IsModuleInitialized(string moduleName);

    /// <summary>
    ///     Clears the initialization state for all modules.
    /// </summary>
    void ClearInitializationState();
}
