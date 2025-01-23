#region

using DropBear.Codex.Blazor.Extensions;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Bases;

/// <summary>
///     An abstract base class for all DropBear components, providing common lifecycle,
///     JS interop, and disposal patterns.
/// </summary>
public abstract class DropBearComponentBase : ComponentBase, IAsyncDisposable
{
    /// <summary>
    ///     Global circuit cancellation token, cancelled if the circuit is disconnected or disposed.
    /// </summary>
    private readonly CancellationTokenSource _circuitCts = new();

    private readonly Dictionary<string, bool> _initializedModules = new();

    private readonly AsyncLock _jsInitLock = new();

    private int _isDisposed; // Tracks disposal state (0 = not disposed, 1 = disposed)

    /// <summary>
    ///     Unique ID assigned to this component instance.
    /// </summary>
    protected string ComponentId { get; } = $"dropbear-{Guid.NewGuid():N}";

    /// <summary>
    ///     Indicates if the component is currently connected (no circuit disconnection).
    /// </summary>
    internal bool IsConnected { get; private set; } = true;

    /// <summary>
    ///     Backing token for all JS calls. Cancelled on circuit disconnection or disposal.
    /// </summary>
    private CancellationToken CircuitToken => _circuitCts.Token;

    /// <summary>
    ///     Indicates whether this component has been disposed.
    /// </summary>
    protected bool IsDisposed => _isDisposed == 1;

    // Injected Services ------------------------------------------------------

    [Inject] protected IJSRuntime JsRuntime { get; set; } = null!;

    [Inject] protected internal ILogger Logger { get; set; } = null!;
    [Inject] protected IJsInitializationService JsInitializationService { get; set; } = null!;

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }

    // Lifecycle Methods ------------------------------------------------------

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            await base.OnInitializedAsync();
            IsConnected = true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during {ComponentName} initialization", GetType().Name);
            throw;
        }
    }

    /// <summary>
    ///     Actual dispose logic. Derived classes can override to release additional resources.
    /// </summary>
    /// <param name="disposing">If true, indicates that we are in a managed disposal scenario.</param>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            // Already disposed
            return;
        }

        if (disposing)
        {
            try
            {
                // Attempt to clean up JS resources if the circuit is still connected
                if (IsConnected && !_circuitCts.IsCancellationRequested)
                {
                    await CleanupJavaScriptResourcesAsync()
                        .WaitAsync(TimeSpan.FromSeconds(2), CircuitToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Debug("Circuit disconnected; skipping JS cleanup in {ComponentName}", GetType().Name);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during {ComponentName} disposal", GetType().Name);
            }
            finally
            {
                _circuitCts.Dispose();
            }
        }
    }

    // JavaScript Interop -----------------------------------------------------

    /// <summary>
    ///     Invokes a JS function returning a result of type <typeparamref name="T" />, with circuit cancellation support.
    /// </summary>
    /// <param name="identifier">The JS function to call.</param>
    /// <param name="args">Additional arguments for the JS function.</param>
    /// <exception cref="ObjectDisposedException">
    ///     Thrown if the component is disposed before or during the call.
    /// </exception>
    /// <returns>The result of the JS invocation.</returns>
    protected async Task<T> SafeJsInteropAsync<T>(string identifier, params object[] args)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        try
        {
            return await JsRuntime
                .InvokeAsync<T>(identifier, CircuitToken, args)
                .WaitAsync(TimeSpan.FromSeconds(5), CircuitToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or OperationCanceledException or TimeoutException)
        {
            IsConnected = false;
            Logger.Warning("JS interop unavailable in {ComponentName}: {Reason}", GetType().Name, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "JS interop call failed in {ComponentName}: {Identifier}", GetType().Name, identifier);
            throw;
        }
    }

    /// <summary>
    ///     Invokes a JS function returning no result (void) with circuit cancellation support.
    /// </summary>
    /// <param name="identifier">The JS function to call.</param>
    /// <param name="args">Additional arguments for the JS function.</param>
    /// <exception cref="ObjectDisposedException">
    ///     Thrown if the component is disposed before or during the call.
    /// </exception>
    protected async Task SafeJsVoidInteropAsync(string identifier, params object[] args)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        try
        {
            await JsRuntime
                .InvokeVoidAsync(identifier, CircuitToken, args)
                .WaitAsync(TimeSpan.FromSeconds(5), CircuitToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or OperationCanceledException or TimeoutException)
        {
            IsConnected = false;
            Logger.Warning("JS interop unavailable in {ComponentName}: {Reason}", GetType().Name, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "JS interop call failed in {ComponentName}: {Identifier}", GetType().Name, identifier);
            throw;
        }
    }

    protected async Task EnsureJsModuleInitializedAsync(string moduleName)
    {
        if (_initializedModules.TryGetValue(moduleName, out var initialized) && initialized)
        {
            return;
        }

        using (await _jsInitLock.LockAsync(CircuitToken))
        {
            if (_initializedModules.TryGetValue(moduleName, out initialized) && initialized)
            {
                return;
            }

            try
            {
                await JsInitializationService.EnsureJsModuleInitializedAsync(moduleName);
                _initializedModules[moduleName] = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize JS module {ModuleName}", moduleName);
                throw;
            }
        }
    }


    /// <summary>
    ///     Override this in a derived class to clean up any JavaScript resources (listeners, etc.).
    /// </summary>
    protected virtual Task CleanupJavaScriptResourcesAsync()
    {
        return Task.CompletedTask;
    }

    // State Management Helpers -----------------------------------------------

    /// <summary>
    ///     Executes a <paramref name="action" /> and then calls <see cref="ComponentBase.StateHasChanged" />.
    /// </summary>
    /// <param name="action">Synchronous action to execute before updating UI.</param>
    protected void InvokeStateHasChanged(Action action)
    {
        try
        {
            action();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error executing state change in {ComponentName}", GetType().Name);
            throw;
        }
    }

    /// <summary>
    ///     Executes an asynchronous <paramref name="action" /> and then calls <see cref="ComponentBase.StateHasChanged" />.
    /// </summary>
    /// <param name="action">Asynchronous function to execute before updating UI.</param>
    protected async Task InvokeStateHasChangedAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error executing state change in {ComponentName}", GetType().Name);
            throw;
        }
    }
}
