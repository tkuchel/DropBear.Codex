#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Core.Results.Base;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Bases;

/// <summary>
///     Abstract base class for DropBear components providing optimized lifecycle management,
///     JavaScript interop, and resource disposal patterns for Blazor Server.
///     Optimized for .NET 9 with improved performance and resource management.
/// </summary>
public abstract class DropBearComponentBase : ComponentBase, IAsyncDisposable
{
    private static readonly TimeSpan JsOperationTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(30);

    // Cache environment detection for better performance
    private static readonly bool IsDevelopment =
        string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                      "Development", StringComparison.OrdinalIgnoreCase);

    // Static diagnostics for tracking components (enabled in Development only)
    private static readonly ConditionalWeakTable<object, LifecycleMonitor> LifecycleMonitors = new();

    private readonly CancellationTokenSource _circuitCts = new();
    private readonly ConcurrentDictionary<string, IJSObjectReference> _jsModuleCache = new();
    private readonly Lock _moduleCacheLock = new(); // .NET 9 Lock class
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
    [Inject] protected Microsoft.Extensions.Logging.ILogger Logger { get; set; } = null!;
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
    private sealed class LifecycleMonitor
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
            if (IsDevelopment)
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
                DisposeJsModulesAsync().AsTask()
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
            _stateChangeSemaphore.Dispose();
            _initializationTcs = null;
        }
    }

    #endregion

    #region JavaScript Interop

    /// <summary>
    ///     Gets a JavaScript module, using the module cache if available.
    ///     Optimized for .NET 9 with improved locking and error handling.
    /// </summary>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="modulePath">The path format for the module.</param>
    /// <param name="caller">The calling method name (automatically populated).</param>
    /// <returns>A result containing the JavaScript module reference or an error.</returns>
    protected async Task<Result<IJSObjectReference, JsInteropError>> GetJsModuleAsync(
        string moduleName,
        string modulePath = "./_content/DropBear.Codex.Blazor/js/{0}.module.js",
        [CallerMemberName] string? caller = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        EnsureNotDisposed();

        try
        {
            // Fast path: check if already cached
            if (_jsModuleCache.TryGetValue(moduleName, out var cachedModule))
            {
                return Result<IJSObjectReference, JsInteropError>.Success(cachedModule);
            }

            // Use .NET 9 Lock for synchronous locking where possible
            lock (_moduleCacheLock)
            {
                // Check again after acquiring the lock
                if (_jsModuleCache.TryGetValue(moduleName, out cachedModule))
                {
                    return Result<IJSObjectReference, JsInteropError>.Success(cachedModule);
                }
            }

            // Async operation needs to be outside the lock
            return await LoadModuleAsync(moduleName, modulePath, caller);
        }
        catch (Exception ex)
        {
            return Result<IJSObjectReference, JsInteropError>.Failure(
                new JsInteropError($"Error in GetJsModuleAsync: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Loads a JavaScript module asynchronously.
    /// </summary>
    private async Task<Result<IJSObjectReference, JsInteropError>> LoadModuleAsync(
        string moduleName,
        string modulePath,
        string? caller)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(JsOperationTimeout);

            var module = await JsRuntime.InvokeAsync<IJSObjectReference>(
                "import", cts.Token, string.Format(modulePath, moduleName));

            // Cache the module
            _jsModuleCache.TryAdd(moduleName, module);

            await JsInitializationService.EnsureJsModuleInitializedAsync(moduleName,
                cancellationToken: cts.Token);

            LogDebug("JS module loaded: {Module} (called from {Caller})", moduleName, caller ?? "Unknown");
            return Result<IJSObjectReference, JsInteropError>.Success(module);
        }
        catch (Exception ex)
        {
            LogError("Failed to load JS module: {Module} (called from {Caller})", ex, moduleName, caller ?? "Unknown");
            return Result<IJSObjectReference, JsInteropError>.Failure(
                new JsInteropError($"Failed to load JS module: {moduleName}"), ex);
        }
    }

    /// <summary>
    ///     Safely invokes a JavaScript function with timeout protection.
    /// </summary>
    /// <typeparam name="T">The return type of the JavaScript function.</typeparam>
    /// <param name="identifier">The JavaScript function identifier.</param>
    /// <param name="caller">The calling method name (automatically populated).</param>
    /// <param name="args">Arguments to pass to the JavaScript function.</param>
    /// <returns>A result containing the JS interop result or an error.</returns>
    protected async Task<Result<T, JsInteropError>> SafeJsInteropAsync<T>(
        string identifier,
        [CallerMemberName] string? caller = null,
        params object[] args)
    {
        try
        {
            EnsureNotDisposed();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(JsOperationTimeout);

            var result = await JsRuntime.InvokeAsync<T>(identifier, cts.Token, args);
            return Result<T, JsInteropError>.Success(result);
        }
        catch (Exception ex)
        {
            HandleJsException(ex, identifier, caller);
            return Result<T, JsInteropError>.Failure(
                new JsInteropError($"JS interop failed for '{identifier}'"), ex);
        }
    }

    /// <summary>
    ///     Safely invokes a JavaScript function with no return value and timeout protection.
    /// </summary>
    /// <param name="identifier">The JavaScript function identifier.</param>
    /// <param name="caller">The calling method name (automatically populated).</param>
    /// <param name="args">Arguments to pass to the JavaScript function.</param>
    /// <returns>A result indicating success or error.</returns>
    protected async Task<Result<Unit, JsInteropError>> SafeJsVoidInteropAsync(
        string identifier,
        [CallerMemberName] string? caller = null,
        params object[] args)
    {
        try
        {
            EnsureNotDisposed();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(JsOperationTimeout);

            await JsRuntime.InvokeVoidAsync(identifier, cts.Token, args);
            return Result<Unit, JsInteropError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            HandleJsException(ex, identifier, caller);
            return Result<Unit, JsInteropError>.Failure(
                new JsInteropError($"JS interop failed for '{identifier}'"), ex);
        }
    }

    #endregion

    #region State Management

    /// <summary>
    ///     Queues a state change action with throttling to prevent excessive renders.
    ///     Optimized for .NET 9 with ValueTask support.
    /// </summary>
    /// <param name="action">The action to perform before updating the UI.</param>
    /// <returns>A ValueTask representing the operation.</returns>
    protected async ValueTask QueueStateHasChangedAsync(Func<ValueTask> action)
    {
        if (IsDisposed) return;

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
    /// <returns>A ValueTask representing the operation.</returns>
    protected async ValueTask QueueStateHasChangedAsync(Action action)
    {
        await QueueStateHasChangedAsync(() =>
        {
            action();
            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    ///     Queues an async Task-based state change action for backward compatibility.
    /// </summary>
    /// <param name="action">The async action to perform before updating the UI.</param>
    /// <returns>A Task representing the operation.</returns>
    protected async Task QueueStateHasChangedAsync(Func<Task> action)
    {
        await QueueStateHasChangedAsync((Func<ValueTask>)(async () =>
        {
            await action().ConfigureAwait(false);
        }));
    }

    #endregion

    #region Resource Management

    /// <summary>
    ///     Disposes all JavaScript modules in the cache.
    /// </summary>
    /// <returns>A result indicating success or error.</returns>
    private async ValueTask<Result<Unit, JsInteropError>> DisposeJsModulesAsync()
    {
        var errors = new List<Exception>();

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
                errors.Add(ex);
            }
        }

        _jsModuleCache.Clear();

        if (errors.Count > 0)
        {
            var combinedMessage = $"Failed to dispose {errors.Count} JS modules";
            var exception = errors.Count > 1 ? new AggregateException(errors) : errors[0];
            return Result<Unit, JsInteropError>.PartialSuccess(
                Unit.Value,
                new JsInteropError(combinedMessage)
                {
                    SourceException = exception,
                    StackTrace = exception.StackTrace
                });
        }

        return Result<Unit, JsInteropError>.Success(Unit.Value);
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
    ///     Performs minimal safe diagnostics on a JavaScript module reference.
    ///     Designed for strict Blazor JS isolation environments where dynamic evaluation is restricted.
    /// </summary>
    /// <param name="module">The JavaScript module reference to test.</param>
    /// <param name="moduleName">A descriptive name for logging.</param>
    /// <param name="functionsToTest">Array of function names to test individually.</param>
    /// <param name="caller">The calling method name (automatically populated).</param>
    /// <returns>A result containing diagnostic information or an error.</returns>
    protected async Task<Result<Dictionary<string, bool>, JsInteropError>> DebugModuleStructureAsync(
        IJSObjectReference module,
        string moduleName = "UnknownModule",
        string[]? functionsToTest = null,
        [CallerMemberName] string? caller = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        EnsureNotDisposed();

        try
        {
            LogDebug("Analyzing module structure: {ModuleName} (called from {Caller})",
                moduleName, caller ?? "unknown");

            var functionResults = new Dictionary<string, bool>();

            // Only test specific functions if provided (simplest approach)
            if (functionsToTest is { Length: > 0 })
            {
                LogDebug("Testing {Count} specific functions:", functionsToTest.Length);

                foreach (var funcName in functionsToTest)
                {
                    var testResult = await TestModuleFunction(module, funcName);
                    functionResults[funcName] = testResult;
                }
            }

            LogDebug("Module analysis complete for: {ModuleName}", moduleName);
            return Result<Dictionary<string, bool>, JsInteropError>.Success(functionResults);
        }
        catch (Exception ex)
        {
            LogError("Module analysis failed (called from {Caller})", ex, caller ?? "unknown");
            return Result<Dictionary<string, bool>, JsInteropError>.Failure(
                new JsInteropError($"Failed to analyze module: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Tests a single module function.
    /// </summary>
    private async Task<bool> TestModuleFunction(IJSObjectReference module, string funcName)
    {
        try
        {
            await module.InvokeVoidAsync(funcName, ComponentToken);
            LogDebug("  - '{FunctionName}' exists and executes without error", funcName);
            return true;
        }
        catch (JSException ex)
        {
            try
            {
                await module.InvokeVoidAsync($"API.{funcName}", ComponentToken);
                LogDebug("  - '{FunctionName}' exists and executes without error", funcName);
                return true;
            }
            catch
            {
                var errorMessage = ex.Message.Contains('\n')
                    ? ex.Message[..ex.Message.IndexOf('\n')]
                    : ex.Message;

                LogDebug("  - '{FunctionName}' error: {Error}", funcName, errorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            LogDebug("  - Error testing '{FunctionName}': {Error}", funcName, ex.Message);
            return false;
        }
    }

    /// <summary>
    ///     Executes an operation safely and returns a Result with success or error information.
    /// </summary>
    /// <typeparam name="T">The type of data to return on success.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="operationName">The name of the operation, used for error reporting.</param>
    /// <param name="timeout">Custom timeout for the operation.</param>
    /// <param name="caller">The calling method name (automatically populated).</param>
    /// <returns>A Result containing either the successful value or error details.</returns>
    protected async Task<Result<T, DataFetchError>> ExecuteSafeResultAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        TimeSpan? timeout = null,
        [CallerMemberName] string? caller = null)
    {
        try
        {
            EnsureNotDisposed();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);
            cts.CancelAfter(timeout ?? DefaultOperationTimeout);

            var result = await operation();
            return Result<T, DataFetchError>.Success(result);
        }
        catch (OperationCanceledException)
        {
            var timeoutSeconds = (timeout ?? DefaultOperationTimeout).TotalSeconds;
            LogWarning("Operation timed out: {Operation} after {Timeout}s", operationName, timeoutSeconds);
            return Result<T, DataFetchError>.Failure(
                DataFetchError.Timeout(operationName ?? "unknown", timeoutSeconds));
        }
        catch (Exception ex)
        {
            LogError("Operation failed: {Operation}", ex, operationName);
            return Result<T, DataFetchError>.Failure(
                DataFetchError.FetchFailed(operationName ?? "unknown", ex.Message), ex);
        }
    }

    /// <summary>
    ///     Ensures the component has not been disposed before executing operations.
    ///     Uses .NET 9's ObjectDisposedException.ThrowIf for improved performance.
    /// </summary>
    /// <param name="caller">The calling method name (automatically populated).</param>
    /// <exception cref="ObjectDisposedException">Thrown if the component has been disposed.</exception>
    private void EnsureNotDisposed([CallerMemberName] string? caller = null)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType().Name);
    }

    /// <summary>
    ///     Handles JavaScript exceptions and updates connection state if needed.
    ///     Optimized with pattern matching for .NET 9.
    /// </summary>
    /// <param name="ex">The exception to handle.</param>
    /// <param name="identifier">The JavaScript function identifier.</param>
    /// <param name="caller">The calling method name.</param>
    /// <returns>True if the exception was handled, false otherwise.</returns>
    private bool HandleJsException(Exception ex, string identifier, string? caller)
    {
        return ex switch
        {
            JSDisconnectedException or TaskCanceledException or ObjectDisposedException =>
                HandleDisconnection(identifier, caller),
            _ => LogAndReturnFalse(ex, identifier, caller)
        };
    }

    /// <summary>
    ///     Handles circuit disconnection scenarios.
    /// </summary>
    private bool HandleDisconnection(string identifier, string? caller)
    {
        if (IsConnected)
        {
            IsConnected = false;
            _circuitStateChanged?.Invoke(this, false);
        }

        LogWarning("JS operation interrupted: {Operation} (called from {Caller})", identifier, caller ?? "Unknown");
        return true;
    }

    /// <summary>
    ///     Logs unhandled JavaScript exceptions.
    /// </summary>
    private bool LogAndReturnFalse(Exception ex, string identifier, string? caller)
    {
        LogError("JS operation failed: {Operation} (called from {Caller})", ex, identifier, caller ?? "Unknown");
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
            _ = InvokeAsync(StateHasChanged);
        }
    }

    #endregion

    #region Logging

    /// <summary>
    ///     Logs an error message with the component type name.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="args">Additional message format arguments.</param>
    protected void LogError(string message, Exception ex, params object[] args)
    {
        Logger.LogError(ex, $"{GetType().Name}: {message}", args);
    }

    /// <summary>
    ///     Logs an error message with the component type name (alternative parameter order).
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="message">The error message.</param>
    /// <param name="args">Additional message format arguments.</param>
    protected void LogError(Exception ex, string message, params object[] args)
    {
        Logger.LogError(ex, $"{GetType().Name}: {message}", args);
    }

    /// <summary>
    ///     Logs a warning message with the component type name.
    /// </summary>
    /// <param name="message">The warning message.</param>
    /// <param name="args">Additional message format arguments.</param>
    protected void LogWarning(string message, params object[] args)
    {
        Logger.LogWarning($"{GetType().Name}: {message}", args);
    }

    /// <summary>
    ///     Logs a debug message with the component type name.
    /// </summary>
    /// <param name="message">The debug message.</param>
    /// <param name="args">Additional message format arguments.</param>
    protected void LogDebug(string message, params object[] args)
    {
        Logger.LogDebug($"{GetType().Name}: {message}", args);
    }

    #endregion
}
