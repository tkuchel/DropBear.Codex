#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Core.Results.Base;
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
    private const int JsOperationTimeoutSeconds = 5;
    private static readonly TimeSpan JsOperationTimeout = TimeSpan.FromSeconds(JsOperationTimeoutSeconds);

    // Static diagnostics for tracking components (enabled in Development only)
    private static readonly ConditionalWeakTable<object, LifecycleMonitor> LifecycleMonitors = new();

    private readonly CancellationTokenSource _circuitCts = new();
    private readonly ConcurrentDictionary<string, IJSObjectReference> _jsModuleCache = new();
    private readonly AsyncLock _moduleCacheLock = new();
    private readonly SemaphoreSlim _stateChangeSemaphore = new(1, 1);
    private EventHandler<bool>? _circuitStateChanged;
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

    /// <summary>
    ///     Event that fires when the circuit connection state changes.
    /// </summary>
    protected event EventHandler<bool> CircuitStateChanged
    {
        add
        {
            if (_circuitStateChanged == null || !_circuitStateChanged.GetInvocationList().Contains(value))
            {
                _circuitStateChanged += value;
            }
        }
        remove => _circuitStateChanged -= value;
    }

    #region Lifecycle Monitoring

    /// <summary>
    ///     Helper class to monitor component lifecycle in development environments.
    /// </summary>
    private class LifecycleMonitor
    {
        private readonly string _componentName;
        private readonly DateTime _createdAt = DateTime.UtcNow;

        public LifecycleMonitor(string componentName)
        {
            _componentName = componentName;
            Debug.WriteLine($"Component created: {_componentName}");
        }

        ~LifecycleMonitor()
        {
            var lifetime = DateTime.UtcNow - _createdAt;
            Debug.WriteLine($"Component finalized: {_componentName} (lifetime: {lifetime})");
        }
    }

    #endregion

    #region Lifecycle Management

    /// <summary>
    ///     Initializes the component asynchronously.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _initializationTcs = new TaskCompletionSource<bool>();

            // In development mode, track component lifecycle
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                LifecycleMonitors.Add(this, new LifecycleMonitor(GetType().Name));
            }

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

    /// <summary>
    ///     Performs post-render initialization.
    /// </summary>
    /// <param name="firstRender">Indicates if this is the first render.</param>
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
    /// <returns>A ValueTask representing the initialization operation.</returns>
    protected virtual ValueTask InitializeComponentAsync()
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Disposes the component asynchronously.
    /// </summary>
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

    /// <summary>
    ///     Core disposal implementation that can be overridden by derived classes.
    /// </summary>
    /// <returns>A ValueTask representing the disposal operation.</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        try
        {
            await _circuitCts.CancelAsync();

            // Create a task that completes when all cleanup is done
            var cleanupTask = Task.WhenAll(
                CleanupJavaScriptResourcesAsync(),
                DisposeJsModulesAsync()
            );

            // Use timeout to prevent hanging
            using var timeoutCts = new CancellationTokenSource(JsOperationTimeout);
            await Task.WhenAny(cleanupTask, Task.Delay(Timeout.Infinite, timeoutCts.Token));
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

    /// <summary>
    ///     Gets a JavaScript module, using the module cache if available.
    /// </summary>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="modulePath">The path format for the module.</param>
    /// <param name="caller">The calling method name (automatically populated).</param>
    /// <returns>A task that resolves to the JavaScript module reference.</returns>
    protected async Task<IJSObjectReference> GetJsModuleAsync(
        string moduleName,
        string modulePath = "./_content/DropBear.Codex.Blazor/js/{0}.module.js",
        [CallerMemberName] string? caller = null)
    {
        EnsureNotDisposed();

        // Fast path: check if already cached
        if (_jsModuleCache.TryGetValue(moduleName, out var cachedModule))
        {
            return cachedModule;
        }

        // Use lock to prevent multiple simultaneous imports of the same module
        using (await _moduleCacheLock.LockAsync(ComponentToken))
        {
            // Check again after acquiring the lock
            if (_jsModuleCache.TryGetValue(moduleName, out cachedModule))
            {
                return cachedModule;
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
                cts.CancelAfter(JsOperationTimeout);

                var module = await JsRuntime
                    .InvokeAsync<IJSObjectReference>(
                        "import",
                        cts.Token,
                        string.Format(modulePath, moduleName));

                _jsModuleCache[moduleName] = module;

                await JsInitializationService.EnsureJsModuleInitializedAsync(moduleName, cancellationToken: cts.Token);

                if (caller != null)
                {
                    LogDebug("JS module loaded: {Module} (called from {Caller})", moduleName, caller);
                }

                return module;
            }
            catch (Exception ex)
            {
                if (caller != null)
                {
                    LogError("Failed to load JS module: {Module} (called from {Caller})", ex, moduleName, caller);
                }

                throw new InvalidOperationException($"Failed to load JS module: {moduleName}", ex);
            }
        }
    }

    /// <summary>
    ///     Safely invokes a JavaScript function with timeout protection.
    /// </summary>
    /// <typeparam name="T">The return type of the JavaScript function.</typeparam>
    /// <param name="identifier">The JavaScript function identifier.</param>
    /// <param name="caller">The calling method name (automatically populated).</param>
    /// <param name="args">Arguments to pass to the JavaScript function.</param>
    /// <returns>A task that resolves to the result of the JavaScript function.</returns>
    protected async Task<T> SafeJsInteropAsync<T>(
        string identifier,
        [CallerMemberName] string? caller = null,
        params object[] args)
    {
        EnsureNotDisposed();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(JsOperationTimeout);

            return await JsRuntime.InvokeAsync<T>(identifier, cts.Token, args);
        }
        catch (Exception ex) when (HandleJsException(ex, identifier, caller))
        {
            throw;
        }
    }

    /// <summary>
    ///     Safely invokes a JavaScript function with no return value and timeout protection.
    /// </summary>
    /// <param name="identifier">The JavaScript function identifier.</param>
    /// <param name="caller">The calling method name (automatically populated).</param>
    /// <param name="args">Arguments to pass to the JavaScript function.</param>
    /// <returns>A task representing the operation.</returns>
    protected async Task SafeJsVoidInteropAsync(
        string identifier,
        [CallerMemberName] string? caller = null,
        params object[] args)
    {
        EnsureNotDisposed();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(JsOperationTimeout);

            await JsRuntime.InvokeVoidAsync(identifier, cts.Token, args);
        }
        catch (Exception ex) when (HandleJsException(ex, identifier, caller))
        {
            throw;
        }
    }

    #endregion

    #region State Management

    /// <summary>
    ///     Queues a state change action with throttling to prevent excessive renders.
    /// </summary>
    /// <param name="action">The action to perform before updating the UI.</param>
    /// <returns>A task representing the operation.</returns>
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

    /// <summary>
    ///     Queues a synchronous state change action.
    /// </summary>
    /// <param name="action">The action to perform before updating the UI.</param>
    /// <returns>A task representing the operation.</returns>
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

    /// <summary>
    ///     Disposes all JavaScript modules in the cache.
    /// </summary>
    /// <returns>A task representing the operation.</returns>
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

    /// <summary>
    ///     Override point for component-specific JavaScript resource cleanup.
    /// </summary>
    /// <returns>A task representing the cleanup operation.</returns>
    protected virtual Task CleanupJavaScriptResourcesAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Analyzes and logs the basic structure of a loaded JavaScript module for debugging purposes.
    ///     This utility helps diagnose JavaScript interop issues in Blazor environments.
    /// </summary>
    /// <param name="module">The JavaScript module reference to analyze.</param>
    /// <param name="moduleName">A descriptive name for the module to include in logs.</param>
    /// <param name="functionsToTest">Array of function names to test for on the module.</param>
    /// <param name="caller">The calling method name (automatically populated).</param>
    /// <returns>A task representing the analysis operation.</returns>
    /// <remarks>
    ///     Use this method during development to troubleshoot JavaScript interop issues.
    ///     It performs safe checks that won't cause exceptions in common Blazor environments.
    /// </remarks>
    protected async Task DebugModuleStructure(
        IJSObjectReference module,
        string moduleName = "UnknownModule",
        string[]? functionsToTest = null,
        [CallerMemberName] string? caller = null)
    {
        if (module == null)
        {
            LogWarning("Cannot debug null module reference (called from {Caller})", caller ?? "unknown");
            return;
        }

        try
        {
            EnsureNotDisposed(caller);

            // Create a linked token with timeout to prevent hanging
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(JsOperationTimeout);

            LogDebug("Analyzing module structure: {ModuleName} (called from {Caller})",
                moduleName, caller ?? "unknown");

            // Test for common ES module indicator
            try
            {
                var moduleType = await module.InvokeAsync<string>("eval", cts.Token, "typeof this");
                LogDebug("Module this context type: {Type}", moduleType);
            }
            catch (Exception ex)
            {
                LogDebug("Unable to determine module context: {Error}", ex.Message);
            }

            // Test specific functions if provided
            if (functionsToTest != null && functionsToTest.Length > 0)
            {
                LogDebug("Testing {Count} specific functions:", functionsToTest.Length);

                foreach (var funcName in functionsToTest)
                {
                    try
                    {
                        // Use a simple test that just checks if invoking the function name throws
                        await module.InvokeVoidAsync("eval", cts.Token, $"typeof {funcName}");
                        LogDebug("  - '{FunctionName}' exists on module", funcName);
                    }
                    catch (JSException ex)
                    {
                        LogDebug("  - '{FunctionName}' not found: {Error}",
                            funcName, ex.Message.Split('\n')[0]);
                    }
                    catch (Exception ex)
                    {
                        LogDebug("  - Error testing '{FunctionName}': {Error}",
                            funcName, ex.Message.Split('\n')[0]);
                    }
                }
            }

            LogDebug("Module analysis complete for: {ModuleName}", moduleName);
        }
        catch (TaskCanceledException)
        {
            LogWarning("Module analysis timed out after {Timeout}s (called from {Caller})",
                JsOperationTimeoutSeconds, caller ?? "unknown");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException)
        {
            LogWarning("Module analysis interrupted: {Reason} (called from {Caller})",
                ex.GetType().Name, caller ?? "unknown");
        }
        catch (Exception ex)
        {
            LogError("Module analysis failed (called from {Caller})", ex, caller ?? "unknown");
        }
    }

    /// <summary>
    ///     Executes an operation safely and returns a Result with success or error information.
    /// </summary>
    /// <typeparam name="T">The type of data to return on success.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="operationName">The name of the operation, used for error reporting.</param>
    /// <param name="caller">The calling method name (automatically populated).</param>
    /// <returns>A Result containing either the successful value or error details.</returns>
    protected async Task<Result<T, DataFetchError>> ExecuteSafeResultAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        [CallerMemberName] string? caller = null)
    {
        try
        {
            EnsureNotDisposed(caller);

            // Use cancellation token for timeout protection
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // Configurable timeout

            var result = await operation();
            return Result<T, DataFetchError>.Success(result);
        }
        catch (OperationCanceledException)
        {
            LogWarning("Operation timed out: {Operation}", operationName);
            return Result<T, DataFetchError>.Failure(
                new DataFetchError("The operation timed out after 30 seconds", operationName));
        }
        catch (Exception ex)
        {
            LogError("Operation failed: {Operation}", ex, operationName);
            return Result<T, DataFetchError>.Failure(
                new DataFetchError($"Failed to execute operation: {ex.Message}", operationName));
        }
    }

    /// <summary>
    ///     Ensures the component has not been disposed before executing operations.
    /// </summary>
    /// <param name="caller">The calling method name (automatically populated).</param>
    /// <exception cref="ObjectDisposedException">Thrown if the component has been disposed.</exception>
    private void EnsureNotDisposed([CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(GetType().Name, $"Component disposed when calling {caller}");
        }
    }

    /// <summary>
    ///     Handles JavaScript exceptions and updates connection state if needed.
    /// </summary>
    /// <param name="ex">The exception to handle.</param>
    /// <param name="identifier">The JavaScript function identifier.</param>
    /// <param name="caller">The calling method name.</param>
    /// <returns>True if the exception was handled, false otherwise.</returns>
    private bool HandleJsException(Exception ex, string identifier, string? caller)
    {
        // Check for connection-related exceptions
        if (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            if (IsConnected)
            {
                IsConnected = false;
                _circuitStateChanged?.Invoke(this, false);
            }

            if (caller != null)
            {
                LogWarning("JS operation interrupted: {Operation} (called from {Caller})", identifier, caller);
            }

            return true;
        }

        if (caller != null)
        {
            LogError("JS operation failed: {Operation} (called from {Caller})", ex, identifier, caller);
        }

        return false;
    }

    /// <summary>
    ///     Called when the circuit is reconnected to restore functionality.
    /// </summary>
    [JSInvokable]
    public virtual void OnCircuitReconnection()
    {
        if (!IsConnected)
        {
            IsConnected = true;
            _circuitStateChanged?.Invoke(this, true);

            // Queue a state change to update the UI after reconnection
            _ = InvokeAsync(() => StateHasChanged());
        }
    }

    /// <summary>
    ///     Logs an error message with the component type name.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="args">Additional message format arguments.</param>
    protected void LogError(string message, Exception ex, params object[] args)
    {
        Logger.Error(ex, $"{GetType().Name}: {message}", args);
    }

    /// <summary>
    ///     Logs a warning message with the component type name.
    /// </summary>
    /// <param name="message">The warning message.</param>
    /// <param name="args">Additional message format arguments.</param>
    protected void LogWarning(string message, params object[] args)
    {
        Logger.Warning($"{GetType().Name}: {message}", args);
    }

    /// <summary>
    ///     Logs a debug message with the component type name.
    /// </summary>
    /// <param name="message">The debug message.</param>
    /// <param name="args">Additional message format arguments.</param>
    protected void LogDebug(string message, params object[] args)
    {
        Logger.Debug($"{GetType().Name}: {message}", args);
    }

    #endregion
}
