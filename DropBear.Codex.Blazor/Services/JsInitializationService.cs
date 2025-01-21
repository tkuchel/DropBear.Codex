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

    public async Task WaitForJsObjectAsync(string objectName, int maxAttempts = 50)
    {
        var lockObj = _initializationLocks.GetOrAdd(objectName, _ => new SemaphoreSlim(1, 1));

        try
        {
            await lockObj.WaitAsync();

            for (var i = 0; i < maxAttempts; i++)
            {
                try
                {
                    var isLoaded = await _jsRuntime.InvokeAsync<bool>(
                        "eval",
                        $"typeof window.{objectName} !== 'undefined' && window.{objectName} !== null"
                    );

                    if (isLoaded)
                    {
                        _logger.Debug("JavaScript object {ObjectName} initialized successfully", objectName);
                        return;
                    }

                    await Task.Delay(100);
                }
                catch (JSException)
                {
                    await Task.Delay(100);
                }
            }

            throw new JSException($"JavaScript object {objectName} failed to initialize after {maxAttempts} attempts");
        }
        finally
        {
            lockObj.Release();
        }
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

            await WaitForJsObjectAsync(moduleName);
            _logger.Debug("JavaScript module {ModuleName} initialized", moduleName);
        }
        catch (OperationCanceledException)
        {
            _logger.Error("Timeout waiting for JavaScript module {ModuleName} to initialize", moduleName);
            throw;
        }
    }
}
