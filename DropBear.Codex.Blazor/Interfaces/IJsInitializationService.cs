using Microsoft.JSInterop;

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Provides thread-safe initialization management for JavaScript modules in Blazor.
/// </summary>
public interface IJsInitializationService : IAsyncDisposable
{
    /// <summary>
    ///     Ensures a JavaScript module is initialized with timeout and cancellation support.
    /// </summary>
    /// <param name="moduleName">The name of the module to initialize.</param>
    /// <param name="timeout">Optional timeout duration (defaults to 5 seconds).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <exception cref="ArgumentException">If moduleName is null or empty.</exception>
    /// <exception cref="ObjectDisposedException">If the service is disposed.</exception>
    /// <exception cref="TimeoutException">If initialization exceeds the timeout period.</exception>
    /// <exception cref="OperationCanceledException">If initialization is cancelled.</exception>
    /// <exception cref="JSException">If module initialization fails.</exception>
    Task EnsureJsModuleInitializedAsync(
        string moduleName,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
