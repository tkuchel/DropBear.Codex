#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Bases;

/// <summary>
///     Abstract base class for DropBear components providing optimized lifecycle management,
///     JavaScript interop, and resource disposal patterns for Blazor Server.
/// </summary>
public abstract class DropBearComponentBase : ComponentBase, IAsyncDisposable
{
    private const int JS_OPERATION_TIMEOUT_SECONDS = 5;
    private readonly CancellationTokenSource _circuitCts = new();
    private readonly ConcurrentDictionary<string, IJSObjectReference> _jsModuleCache = new();
    private readonly AsyncLock _moduleCacheLock = new();
    private readonly SemaphoreSlim _stateChangeSemaphore = new(1, 1);
    private TaskCompletionSource<bool>? _initializationTcs;
    private int _isDisposed;
    private volatile bool _isStateChangeQueued;

    /// <summary>
    ///     Unique component identifier using a thread-safe initialization pattern.
    /// </summary>
    protected string ComponentId { get; } = $"dropbear-{Guid.NewGuid():N}";

    /// <summary>
    ///     Thread-safe connection state tracking.
    /// </summary>
    protected bool IsConnected { get; private set; } = true;

    /// <summary>
    ///     Cancellation token for component operations.
    /// </summary>
    protected CancellationToken ComponentToken => _circuitCts.Token;

    /// <summary>
    ///     Thread-safe disposal state.
    /// </summary>
    protected bool IsDisposed => Volatile.Read(ref _isDisposed) == 1;

    [Inject] protected IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] protected ILogger Logger { get; set; } = null!;
    [Inject] protected IJsInitializationService JsInitializationService { get; set; } = null!;

    #region Lifecycle Management

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _initializationTcs = new TaskCompletionSource<bool>();
            await base.OnInitializedAsync();
            IsConnected = true;
            _initializationTcs.TrySetResult(true);
        }
        catch (Exception ex)
        {
            LogError("Component initialization failed", ex);
            _initializationTcs?.TrySetException(ex);
            throw;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            try
            {
                await InitializeComponentAsync();
            }
            catch (Exception ex)
            {
                LogError("Component post-render initialization failed", ex);
            }
        }
    }

    /// <summary>
    ///     Override point for component-specific initialization after first render.
    /// </summary>
    protected virtual Task InitializeComponentAsync()
    {
        return Task.CompletedTask;
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }
        catch (Exception ex)
        {
            LogError("Disposal error", ex);
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        try
        {
            await _circuitCts.CancelAsync();

            var cleanupTask = Task.WhenAll(
                CleanupJavaScriptResourcesAsync(),
                DisposeJsModulesAsync()
            );

            // Use timeout to prevent hanging
            await Task.WhenAny(cleanupTask, Task.Delay(TimeSpan.FromSeconds(JS_OPERATION_TIMEOUT_SECONDS), ComponentToken));
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Cleanup interrupted: {Reason}", ex.GetType().Name);
        }
        finally
        {
            _circuitCts.Dispose();
            _moduleCacheLock.Dispose();
            _stateChangeSemaphore.Dispose();
            _initializationTcs = null;
        }
    }

    #endregion

    #region JavaScript Interop

    protected async Task<IJSObjectReference> GetJsModuleAsync(
        string moduleName,
        string modulePath = "./_content/DropBear.Codex.Blazor/js/{0}.module.js",
        [CallerMemberName] string? caller = null)
    {
        EnsureNotDisposed();

        if (_jsModuleCache.TryGetValue(moduleName, out var cachedModule))
        {
            return cachedModule;
        }

        using (await _moduleCacheLock.LockAsync(ComponentToken))
        {
            if (_jsModuleCache.TryGetValue(moduleName, out cachedModule))
            {
                return cachedModule;
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
                cts.CancelAfter(TimeSpan.FromSeconds(JS_OPERATION_TIMEOUT_SECONDS));

                var module = await JsRuntime
                    .InvokeAsync<IJSObjectReference>(
                        "import",
                        cts.Token,
                        string.Format(modulePath, moduleName));

                _jsModuleCache[moduleName] = module;

                await JsInitializationService.EnsureJsModuleInitializedAsync(moduleName);

                LogDebug("JS module loaded: {Module} (called from {Caller})", moduleName, caller);
                return module;
            }
            catch (Exception ex)
            {
                LogError("Failed to load JS module: {Module} (called from {Caller})", ex, moduleName, caller);
                throw new InvalidOperationException($"Failed to load JS module: {moduleName}", ex);
            }
        }
    }

    protected async Task<T> SafeJsInteropAsync<T>(
        string identifier,
        [CallerMemberName] string? caller = null,
        params object[] args)
    {
        EnsureNotDisposed();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(TimeSpan.FromSeconds(JS_OPERATION_TIMEOUT_SECONDS));

            return await JsRuntime.InvokeAsync<T>(identifier, cts.Token, args);
        }
        catch (Exception ex) when (HandleJsException(ex, identifier, caller))
        {
            throw;
        }
    }

    protected async Task SafeJsVoidInteropAsync(
        string identifier,
        [CallerMemberName] string? caller = null,
        params object[] args)
    {
        EnsureNotDisposed();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(TimeSpan.FromSeconds(JS_OPERATION_TIMEOUT_SECONDS));

            await JsRuntime.InvokeVoidAsync(identifier, cts.Token, args);
        }
        catch (Exception ex) when (HandleJsException(ex, identifier, caller))
        {
            throw;
        }
    }

    #endregion

    #region State Management

    protected async Task QueueStateHasChangedAsync(Func<Task> action)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            await _stateChangeSemaphore.WaitAsync(ComponentToken);

            if (!_isStateChangeQueued)
            {
                _isStateChangeQueued = true;
                try
                {
                    await action();
                    await InvokeAsync(StateHasChanged);
                }
                finally
                {
                    _isStateChangeQueued = false;
                }
            }
        }
        finally
        {
            _stateChangeSemaphore.Release();
        }
    }

    protected async Task QueueStateHasChangedAsync(Action action)
    {
        await QueueStateHasChangedAsync(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    #endregion

    #region Resource Management

    private async Task DisposeJsModulesAsync()
    {
        foreach (var (moduleName, moduleRef) in _jsModuleCache)
        {
            try
            {
                await moduleRef.DisposeAsync();
                LogDebug("Disposed JS module: {Module}", moduleName);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
            {
                LogWarning("JS module {Module} disposal interrupted: {Reason}", moduleName, ex.GetType().Name);
            }
            catch (Exception ex)
            {
                LogError("Error disposing JS module: {Module}", ex, moduleName);
            }
        }

        _jsModuleCache.Clear();
    }

    protected virtual Task CleanupJavaScriptResourcesAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Helper Methods

    private void EnsureNotDisposed([CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(GetType().Name, $"Component disposed when calling {caller}");
        }
    }

    private bool HandleJsException(Exception ex, string identifier, string? caller)
    {
        if (ex is JSDisconnectedException or TaskCanceledException)
        {
            IsConnected = false;
            LogWarning("JS operation interrupted: {Operation} (called from {Caller})", identifier, caller);
            return true;
        }

        LogError("JS operation failed: {Operation} (called from {Caller})", ex, identifier, caller);
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
