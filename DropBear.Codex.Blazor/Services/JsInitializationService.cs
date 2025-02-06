#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Interfaces;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Provides services to ensure JavaScript modules are properly initialized.
/// </summary>
public class JsInitializationService : IJsInitializationService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _initializationLocks = new();
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsInitializationService" /> class.
    /// </summary>
    /// <param name="jsRuntime">The JavaScript runtime instance.</param>
    /// <param name="logger">The logger instance.</param>
    public JsInitializationService(IJSRuntime jsRuntime, ILogger logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    ///     Ensures that the specified JavaScript module is initialized within an optional timeout period.
    /// </summary>
    /// <param name="moduleName">Name of the JavaScript module to initialize.</param>
    /// <param name="timeout">Optional timeout period. Defaults to 5 seconds.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the initialization times out.</exception>
    public async Task EnsureJsModuleInitializedAsync(string moduleName, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        using var cts = new CancellationTokenSource(timeout.Value);
        var cancellationToken = cts.Token;

        // Get or create the semaphore for the module.
        var lockObj = _initializationLocks.GetOrAdd(moduleName, _ => new SemaphoreSlim(1, 1));

        // Wait to acquire the semaphore while observing cancellation.
        await lockObj.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Poll until the module is fully initialized.
            await WaitForModuleInitializationAsync(moduleName, cancellationToken).ConfigureAwait(false);
            _logger.Debug("JavaScript module {ModuleName} initialization confirmed", moduleName);
        }
        finally
        {
            // Always release the semaphore.
            lockObj.Release();
        }
    }

    /// <summary>
    ///     Waits for the specified JavaScript module to be fully initialized.
    /// </summary>
    /// <param name="moduleName">Name of the JavaScript module.</param>
    /// <param name="cancellationToken">Cancellation token to observe.</param>
    /// <param name="maxAttempts">Maximum number of polling attempts. Defaults to 50.</param>
    /// <returns>A task representing the asynchronous wait operation.</returns>
    /// <exception cref="JSException">
    ///     Thrown if the module fails to initialize after the specified number of attempts.
    /// </exception>
    private async Task WaitForModuleInitializationAsync(string moduleName, CancellationToken cancellationToken,
        int maxAttempts = 50)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Check for module existence and initialization state.
                var isInitialized = await _jsRuntime.InvokeAsync<bool>(
                    "eval",
                    cancellationToken,
                    $@"
                        typeof window['{moduleName}'] !== 'undefined' &&
                        window['{moduleName}'] !== null &&
                        (window['{moduleName}'].__initialized === true ||
                         typeof window['{moduleName}'].initialize === 'function')
                    ").ConfigureAwait(false);

                if (isInitialized)
                {
                    // Determine if the module still requires initialization.
                    var needsInitialization = await _jsRuntime.InvokeAsync<bool>(
                        "eval",
                        cancellationToken,
                        $@"
                            typeof window['{moduleName}'] !== 'undefined' &&
                            window['{moduleName}'] !== null &&
                            window['{moduleName}'].__initialized !== true &&
                            typeof window['{moduleName}'].initialize === 'function'
                        ").ConfigureAwait(false);

                    if (needsInitialization)
                    {
                        // Invoke the initialization function and continue polling.
                        await _jsRuntime.InvokeVoidAsync($"{moduleName}.initialize", cancellationToken)
                            .ConfigureAwait(false);
                        continue;
                    }

                    // The module is fully initialized.
                    return;
                }

                // Wait briefly before the next polling attempt.
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            catch (JSException)
            {
                // On JS interop errors, wait briefly before retrying.
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new JSException($"JavaScript module {moduleName} failed to initialize after {maxAttempts} attempts");
    }
}
