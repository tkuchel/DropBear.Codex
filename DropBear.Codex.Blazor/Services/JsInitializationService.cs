#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Interfaces;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

public class JsInitializationService : IJsInitializationService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _initializationLocks = new();
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger _logger;

    public JsInitializationService(IJSRuntime jsRuntime, ILogger logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task EnsureJsModuleInitializedAsync(string moduleName, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        using var cts = new CancellationTokenSource(timeout.Value);

        try
        {
            var lockObj = _initializationLocks.GetOrAdd(moduleName, _ => new SemaphoreSlim(1, 1));

            await using var registration = cts.Token.Register(() => lockObj.Release());
            await lockObj.WaitAsync(cts.Token);

            try
            {
                await WaitForModuleInitializationAsync(moduleName);
                _logger.Debug("JavaScript module {ModuleName} initialization confirmed", moduleName);
            }
            finally
            {
                lockObj.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Error("Timeout waiting for JavaScript module {ModuleName} to initialize", moduleName);
            throw;
        }
    }

    private async Task WaitForModuleInitializationAsync(string moduleName, int maxAttempts = 50)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            try
            {
                // Check both for module existence and initialization state
                var isInitialized = await _jsRuntime.InvokeAsync<bool>(
                    "eval",
                    $@"
                    typeof window['{moduleName}'] !== 'undefined' &&
                    window['{moduleName}'] !== null &&
                    (window['{moduleName}'].__initialized === true ||
                     typeof window['{moduleName}'].initialize === 'function')
                "
                );

                if (isInitialized)
                {
                    // If module exists but isn't initialized, try to initialize it
                    var needsInitialization = await _jsRuntime.InvokeAsync<bool>(
                        "eval",
                        $@"
                        typeof window['{moduleName}'] !== 'undefined' &&
                        window['{moduleName}'] !== null &&
                        window['{moduleName}'].__initialized !== true &&
                        typeof window['{moduleName}'].initialize === 'function'
                    "
                    );

                    if (needsInitialization)
                    {
                        await _jsRuntime.InvokeVoidAsync($"{moduleName}.initialize");
                        continue; // Continue checking for initialization
                    }

                    return; // Module is fully initialized
                }

                await Task.Delay(100);
            }
            catch (JSException)
            {
                await Task.Delay(100);
            }
        }

        throw new JSException($"JavaScript module {moduleName} failed to initialize after {maxAttempts} attempts");
    }
}
