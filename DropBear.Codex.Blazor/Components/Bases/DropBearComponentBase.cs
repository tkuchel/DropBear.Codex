#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Extensions;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Bases;

/// <summary>
///     Abstract base class for DropBear components providing common lifecycle management,
///     JavaScript interop, and resource disposal patterns.
/// </summary>
public abstract class DropBearComponentBase : ComponentBase, IAsyncDisposable
{
    #region Fields and Properties

    private readonly CancellationTokenSource _circuitCts = new();
    private readonly AsyncLock _moduleCacheLock = new();
    private readonly ConcurrentDictionary<string, IJSObjectReference> _jsModuleCache = new();
    private int _isDisposed;

    /// <summary>
    ///     Unique component identifier for DOM interactions
    /// </summary>
    protected string ComponentId { get; } = $"dropbear-{Guid.NewGuid():N}";

    /// <summary>
    ///     Indicates if the component is connected to an active circuit
    /// </summary>
    protected bool IsConnected { get; private set; } = true;

    /// <summary>
    ///     Cancellation token tied to component lifecycle
    /// </summary>
    protected CancellationToken ComponentToken => _circuitCts.Token;

    /// <summary>
    ///     Flag indicating if the component has been disposed
    /// </summary>
    protected bool IsDisposed => _isDisposed == 1;

    [Inject] protected IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] protected ILogger Logger { get; set; } = null!;
    [Inject] protected IJsInitializationService JsInitializationService { get; set; } = null!;

    #endregion

    #region Lifecycle Management

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await base.OnInitializedAsync().ConfigureAwait(false);
            IsConnected = true;
        }
        catch (Exception ex)
        {
            LogError("Component initialization failed", ex);
            throw;
        }
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            await DisposeCoreAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
        catch (Exception ex)
        {
            LogError("Disposal error", ex);
        }
    }

    protected virtual async ValueTask DisposeCoreAsync()
    {
        await _circuitCts.CancelAsync();

        try
        {
            await CleanupJavaScriptResourcesAsync().ConfigureAwait(false);
            await DisposeJsModulesAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            LogWarning("JS disconnected during cleanup");
        }
        finally
        {
            _circuitCts.Dispose();
            _moduleCacheLock.Dispose();
        }
    }

    #endregion

    #region JavaScript Interop

    /// <summary>
    ///     Safely retrieves a JavaScript module reference with caching and initialization
    /// </summary>
    /// <param name="moduleName">Name of the JS module to load</param>
    /// <param name="modulePath">Path to the module file (defaults to standard location)</param>
    /// <returns>Cached JavaScript module reference</returns>
    protected async Task<IJSObjectReference> GetJsModuleAsync(
        string moduleName,
        string modulePath = "./_content/DropBear.Codex.Blazor/js/{0}.module.js")
    {
        EnsureNotDisposed();

        if (_jsModuleCache.TryGetValue(moduleName, out var cachedModule))
        {
            return cachedModule;
        }

        using (await _moduleCacheLock.LockAsync(ComponentToken).ConfigureAwait(false))
        {
            if (_jsModuleCache.TryGetValue(moduleName, out cachedModule))
            {
                return cachedModule;
            }

            try
            {
                // First, load the module
                var module = await JsRuntime.InvokeAsync<IJSObjectReference>(
                        "import",
                        ComponentToken,
                        string.Format(modulePath, moduleName))
                    .WaitAsync(TimeSpan.FromSeconds(5), ComponentToken)
                    .ConfigureAwait(false);

                _jsModuleCache[moduleName] = module;

                // Then, ensure initialization
                await JsInitializationService.EnsureJsModuleInitializedAsync(moduleName)
                    .WaitAsync(TimeSpan.FromSeconds(5), ComponentToken)
                    .ConfigureAwait(false);

                return module;
            }
            catch (OperationCanceledException)
            {
                LogDebug("Module load cancelled: {Module}", moduleName);
                throw;
            }
            catch (Exception ex)
            {
                LogError("Failed to load JS module: {Module}", ex, moduleName);
                throw new InvalidOperationException($"JS module '{moduleName}' load failed", ex);
            }
        }
    }

    /// <summary>
    ///     Safely invokes a JavaScript function with result handling
    /// </summary>
    protected async Task<T> SafeJsInteropAsync<T>(string identifier, params object[] args)
    {
        EnsureNotDisposed();

        try
        {
            return await JsRuntime.InvokeAsync<T>(
                    identifier,
                    ComponentToken,
                    args)
                .WaitAsync(TimeSpan.FromSeconds(5), ComponentToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (HandleJsException(ex, identifier))
        {
            throw;
        }
    }

    /// <summary>
    ///     Safely invokes a JavaScript void function
    /// </summary>
    protected async Task SafeJsVoidInteropAsync(string identifier, params object[] args)
    {
        EnsureNotDisposed();

        try
        {
            await JsRuntime.InvokeVoidAsync(
                    identifier,
                    ComponentToken,
                    args)
                .WaitAsync(TimeSpan.FromSeconds(5), ComponentToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (HandleJsException(ex, identifier))
        {
            throw;
        }
    }

    protected async Task EnsureJsModuleInitializedAsync(string moduleName)
    {
        EnsureNotDisposed();

        try
        {
            await JsInitializationService.EnsureJsModuleInitializedAsync(moduleName)
                .WaitAsync(TimeSpan.FromSeconds(5), ComponentToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            LogDebug("Module initialization cancelled: {Module}", moduleName);
            throw;
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize JS module: {Module}", ex, moduleName);
            throw new InvalidOperationException($"JS module '{moduleName}' initialization failed", ex);
        }
    }

    #endregion

    #region Resource Management

    /// <summary>
    ///     Disposes all cached JavaScript module references
    /// </summary>
    private async ValueTask DisposeJsModulesAsync()
    {
        foreach (var (moduleName, moduleRef) in _jsModuleCache)
        {
            try
            {
                await moduleRef.DisposeAsync().ConfigureAwait(false);
                LogDebug("Disposed JS module: {Module}", moduleName);
            }
            catch (Exception ex)
            {
                LogError("Error disposing JS module: {Module}", ex, moduleName);
            }
        }

        _jsModuleCache.Clear();
    }

    /// <summary>
    ///     Override point for component-specific JavaScript cleanup
    /// </summary>
    protected virtual Task CleanupJavaScriptResourcesAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region State Management

    /// <summary>
    ///     Executes an action and triggers UI update
    /// </summary>
    protected void InvokeStateHasChanged(Action action)
    {
        EnsureNotDisposed();
        try
        {
            action();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            LogError("State update failed", ex);
            throw;
        }
    }

    /// <summary>
    ///     Executes an async operation and triggers UI update
    /// </summary>
    protected async Task InvokeStateHasChangedAsync(Func<Task> action)
    {
        EnsureNotDisposed();
        try
        {
            await action().ConfigureAwait(false);
            await InvokeAsync(StateHasChanged).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogError("Async state update failed", ex);
            throw;
        }
    }

    #endregion

    #region Helpers

    private void EnsureNotDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    private bool HandleJsException(Exception ex, string identifier)
    {
        if (ex is JSDisconnectedException or TaskCanceledException)
        {
            IsConnected = false;
            LogWarning("JS disconnected during operation: {Operation}", identifier);
            return true;
        }

        LogError("JS operation failed: {Operation}", ex, identifier);
        return false;
    }

    protected void LogError(string message, Exception ex, params object[] args)
    {
        Logger.Error(ex, $"{GetType().Name}: {message}", args);
    }

    protected void LogWarning(string message, params object[] args)
    {
        Logger.Warning($"{GetType().Name}: {message}", args);
    }

    protected void LogDebug(string message, params object[] args)
    {
        Logger.Debug($"{GetType().Name}: {message}", args);
    }

    #endregion
}
