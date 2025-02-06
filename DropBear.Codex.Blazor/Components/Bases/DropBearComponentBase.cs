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
    ///     Unique component identifier for DOM interactions.
    /// </summary>
    protected string ComponentId { get; } = $"dropbear-{Guid.NewGuid():N}";

    /// <summary>
    ///     Indicates if the component is connected to an active circuit.
    /// </summary>
    protected bool IsConnected { get; private set; } = true;

    /// <summary>
    ///     Cancellation token tied to component lifecycle.
    /// </summary>
    protected CancellationToken ComponentToken => _circuitCts.Token;

    /// <summary>
    ///     Flag indicating if the component has been disposed.
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
            // Ensure the base component initialization is complete.
            await base.OnInitializedAsync().ConfigureAwait(false);
            IsConnected = true;
        }
        catch (Exception ex)
        {
            LogError("Component initialization failed", ex);
            throw;
        }
    }

    /// <summary>
    ///     **[Deprecated Override Point]**
    ///     This method is provided for backward compatibility. Derived classes should override <see cref="DisposeAsyncCore" />
    ///     instead.
    /// </summary>
    /// <param name="disposing">Indicates whether the method is being called during disposal.</param>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            // Note: This method is not used by the base implementation.
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Public asynchronous disposal method following the IAsyncDisposable pattern.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        // Ensure disposal happens only once.
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            // Call the core disposal logic.
            await DisposeAsyncCore().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
        catch (Exception ex)
        {
            LogError("Disposal error", ex);
        }
    }

    /// <summary>
    ///     Core disposal logic that derived classes can override to add custom disposal steps.
    /// </summary>
    /// <remarks>
    ///     Derived classes should override this method instead of <see cref="DisposeAsync(bool)" />.
    /// </remarks>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        // Cancel any ongoing operations associated with this component.
        await _circuitCts.CancelAsync().ConfigureAwait(false);

        try
        {
            // Allow derived components to clean up their JavaScript resources.
            await CleanupJavaScriptResourcesAsync().ConfigureAwait(false);
            // Dispose all cached JS modules.
            await DisposeJsModulesAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            LogWarning("Cleanup skipped: JS runtime disconnected.");
        }
        catch (TaskCanceledException)
        {
            LogWarning("Cleanup skipped: Operation cancelled.");
        }
        finally
        {
            // Dispose managed resources.
            _circuitCts.Dispose();
            _moduleCacheLock.Dispose();
        }
    }

    #endregion

    #region JavaScript Interop

    /// <summary>
    ///     Safely retrieves a JavaScript module reference with caching and initialization.
    /// </summary>
    /// <param name="moduleName">Name of the JS module to load.</param>
    /// <param name="modulePath">
    ///     Path to the module file (defaults to standard location).
    ///     Use string.Format notation to insert the module name.
    /// </param>
    /// <returns>Cached JavaScript module reference.</returns>
    protected async Task<IJSObjectReference> GetJsModuleAsync(
        string moduleName,
        string modulePath = "./_content/DropBear.Codex.Blazor/js/{0}.module.js")
    {
        EnsureNotDisposed();

        if (_jsModuleCache.TryGetValue(moduleName, out var cachedModule))
        {
            return cachedModule;
        }

        // Use an asynchronous lock to ensure that only one load operation occurs per module.
        using (await _moduleCacheLock.LockAsync(ComponentToken).ConfigureAwait(false))
        {
            if (_jsModuleCache.TryGetValue(moduleName, out cachedModule))
            {
                return cachedModule;
            }

            try
            {
                // Load the JS module.
                var module = await JsRuntime
                    .InvokeAsync<IJSObjectReference>(
                        "import",
                        ComponentToken,
                        string.Format(modulePath, moduleName))
                    .WaitAsync(TimeSpan.FromSeconds(5), ComponentToken)
                    .ConfigureAwait(false);

                _jsModuleCache[moduleName] = module;

                // Ensure that the module is properly initialized.
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
    ///     Safely invokes a JavaScript function with result handling.
    /// </summary>
    protected async Task<T> SafeJsInteropAsync<T>(string identifier, params object[] args)
    {
        EnsureNotDisposed();

        try
        {
            return await JsRuntime
                .InvokeAsync<T>(identifier, ComponentToken, args)
                .WaitAsync(TimeSpan.FromSeconds(5), ComponentToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (HandleJsException(ex, identifier))
        {
            throw;
        }
    }

    /// <summary>
    ///     Safely invokes a JavaScript void function.
    /// </summary>
    protected async Task SafeJsVoidInteropAsync(string identifier, params object[] args)
    {
        EnsureNotDisposed();

        try
        {
            await JsRuntime
                .InvokeVoidAsync(identifier, ComponentToken, args)
                .WaitAsync(TimeSpan.FromSeconds(5), ComponentToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (HandleJsException(ex, identifier))
        {
            throw;
        }
    }

    /// <summary>
    ///     Ensures that the specified JavaScript module is initialized.
    /// </summary>
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
    ///     Disposes all cached JavaScript module references.
    ///     Exceptions due to circuit disconnection or cancellation are logged as warnings.
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
            catch (JSDisconnectedException)
            {
                LogWarning("JS module {Module} disposal skipped due to circuit disconnection", moduleName);
            }
            catch (TaskCanceledException)
            {
                LogWarning("JS module {Module} disposal skipped due to cancellation", moduleName);
            }
            catch (Exception ex)
            {
                LogError("Error disposing JS module: {Module}", ex, moduleName);
            }
        }

        _jsModuleCache.Clear();
    }

    /// <summary>
    ///     Override point for component-specific JavaScript cleanup.
    /// </summary>
    protected virtual Task CleanupJavaScriptResourcesAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region State Management

    /// <summary>
    ///     Executes an action and triggers a UI update.
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
    ///     Executes an asynchronous operation and triggers a UI update.
    /// </summary>
    protected async Task InvokeStateHasChangedAsync(Func<Task> action)
    {
        EnsureNotDisposed();
        try
        {
            await action().ConfigureAwait(false);
            // Ensure the UI update occurs on the correct synchronization context.
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

    /// <summary>
    ///     Throws an ObjectDisposedException if the component has already been disposed.
    /// </summary>
    private void EnsureNotDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    /// <summary>
    ///     Handles JavaScript interop exceptions, setting the connection state and logging appropriately.
    /// </summary>
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

    /// <summary>
    ///     Logs errors with the component type name as context.
    /// </summary>
    protected void LogError(string message, Exception ex, params object[] args)
    {
        Logger.Error(ex, $"{GetType().Name}: {message}", args);
    }

    /// <summary>
    ///     Logs warnings with the component type name as context.
    /// </summary>
    protected void LogWarning(string message, params object[] args)
    {
        Logger.Warning($"{GetType().Name}: {message}", args);
    }

    /// <summary>
    ///     Logs debug information with the component type name as context.
    /// </summary>
    protected void LogDebug(string message, params object[] args)
    {
        Logger.Debug($"{GetType().Name}: {message}", args);
    }

    #endregion
}
