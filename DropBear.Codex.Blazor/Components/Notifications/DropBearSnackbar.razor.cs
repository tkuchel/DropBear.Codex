#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

/// <summary>
///     Modern snackbar component optimized for .NET 8+ and Blazor Server.
///     Provides smooth animations, accessibility support, and responsive design.
/// </summary>
public sealed partial class DropBearSnackbar : DropBearComponentBase
{
    #region Constants

    private const string JsModuleName = "snackbar";
    private const int DefaultDuration = 5000;
    private const int MaxRetries = 3;

    #endregion

    #region Fields

    private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<DropBearSnackbar>? _dotNetRef;
    private CancellationTokenSource? _timeoutCts;

    private volatile bool _isInitialized;
    private volatile bool _isVisible;
    private int _retryCount;

    // Cached values for performance
    private string? _cachedCssClasses;
    private SnackbarType _cachedType = (SnackbarType)(-1);

    #endregion

    #region Parameters

    /// <summary>
    ///     Gets or sets the snackbar instance containing all display information.
    /// </summary>
    [Parameter, EditorRequired]
    public required SnackbarInstance SnackbarInstance { get; init; }

    /// <summary>
    ///     Event callback invoked when the snackbar is closed.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>
    ///     Additional HTML attributes for the container.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the CSS classes with performance caching.
    /// </summary>
    private string CssClasses
    {
        get
        {
            if (_cachedType != SnackbarInstance.Type || _cachedCssClasses is null)
            {
                _cachedType = SnackbarInstance.Type;
                _cachedCssClasses = $"dropbear-snackbar {GetTypeClass()} {GetVisibilityClass()}";
            }

            return _cachedCssClasses;
        }
    }

    /// <summary>
    ///     Gets whether the snackbar should auto-close.
    /// </summary>
    private bool ShouldAutoClose => !SnackbarInstance.RequiresManualClose && SnackbarInstance.Duration > 0;

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Initializes the component after first render.
    /// </summary>
    protected override async ValueTask InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed) return;

        await ExecuteWithRetry(async () =>
        {
            await _operationSemaphore.WaitAsync(ComponentToken);
            try
            {
                await InitializeJavaScriptAsync();
                await ShowSnackbarAsync();

                if (ShouldAutoClose)
                {
                    StartAutoCloseTimer();
                }

                _isInitialized = true;
                _isVisible = true;

                LogDebug("Snackbar initialized successfully: {Id}", SnackbarInstance.Id);
            }
            finally
            {
                _operationSemaphore.Release();
            }
        });
    }

    /// <summary>
    ///     Optimized render control using cached state.
    /// </summary>
    protected override bool ShouldRender()
    {
        return !IsDisposed && (_cachedCssClasses is null || _cachedType != SnackbarInstance.Type);
    }

    /// <summary>
    ///     Cleanup resources on disposal.
    /// </summary>
    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            _timeoutCts?.Cancel();

            await _operationSemaphore.WaitAsync(TimeSpan.FromSeconds(2));
            try
            {
                await CleanupJavaScriptAsync();
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        catch (Exception ex) when (ex is TaskCanceledException or ObjectDisposedException)
        {
            LogWarning("Cleanup interrupted: {Reason}", ex.GetType().Name);
        }
        finally
        {
            _dotNetRef?.Dispose();
            _timeoutCts?.Dispose();
            _operationSemaphore.Dispose();

            _dotNetRef = null;
            _jsModule = null;
            _timeoutCts = null;
        }

        await base.DisposeAsyncCore();
    }

    #endregion

    #region JavaScript Interop

    /// <summary>
    ///     Initializes JavaScript module and setup.
    /// </summary>
    private async Task InitializeJavaScriptAsync()
    {
        var moduleResult = await GetJsModuleAsync(JsModuleName);
        if (moduleResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to load JS module: {moduleResult.Exception?.Message}");
        }

        _jsModule = moduleResult.Value;
        _dotNetRef = DotNetObjectReference.Create(this);

        // Initialize with optimized single call
        await _jsModule.InvokeVoidAsync("initialize", ComponentToken,
            SnackbarInstance.Id, _dotNetRef, SnackbarInstance);
    }

    /// <summary>
    ///     Shows the snackbar with animation.
    /// </summary>
    private async Task ShowSnackbarAsync()
    {
        if (_jsModule is null) return;

        await _jsModule.InvokeVoidAsync("show", ComponentToken, SnackbarInstance.Id);
        _isVisible = true;

        // Update CSS classes to reflect visibility
        _cachedCssClasses = null;
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    ///     Hides the snackbar with animation.
    /// </summary>
    private async Task HideSnackbarAsync()
    {
        if (_jsModule is null || !_isVisible) return;

        await _jsModule.InvokeVoidAsync("hide", ComponentToken, SnackbarInstance.Id);
        _isVisible = false;

        // Update CSS classes to reflect visibility
        _cachedCssClasses = null;
    }

    /// <summary>
    ///     Cleanup JavaScript resources.
    /// </summary>
    private async Task CleanupJavaScriptAsync()
    {
        if (_jsModule is null) return;

        try
        {
            await _jsModule.InvokeVoidAsync("dispose", ComponentToken, SnackbarInstance.Id);
        }
        catch (JSException ex) when (IsElementNotFoundError(ex))
        {
            LogDebug("Element already removed: {Id}", SnackbarInstance.Id);
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    ///     Handles action button clicks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task HandleActionClickAsync(SnackbarAction action)
    {
        if (!_isInitialized || IsDisposed) return;

        try
        {
            if (action.OnClick is not null)
            {
                await action.OnClick.Invoke();
            }

            await CloseAsync();
        }
        catch (Exception ex)
        {
            LogError("Action click failed: {Label}", ex, action.Label);
            // Always attempt to close on error
            await CloseAsync();
        }
    }

    /// <summary>
    ///     Closes the snackbar and invokes the close callback.
    /// </summary>
    public async Task CloseAsync()
    {
        if (!_isInitialized || IsDisposed) return;

        try
        {
            _timeoutCts?.Cancel();

            await _operationSemaphore.WaitAsync(ComponentToken);
            try
            {
                await HideSnackbarAsync();

                if (OnClose.HasDelegate)
                {
                    await OnClose.InvokeAsync();
                }

                LogDebug("Snackbar closed: {Id}", SnackbarInstance.Id);
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            LogError("Failed to close snackbar: {Id}", ex, SnackbarInstance.Id);
        }
    }

    /// <summary>
    ///     JavaScript callback when auto-close timer expires.
    /// </summary>
    [JSInvokable]
    public async Task OnAutoCloseAsync()
    {
        await InvokeAsync(CloseAsync);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Starts the auto-close timer for snackbars that should auto-dismiss.
    /// </summary>
    private void StartAutoCloseTimer()
    {
        if (!ShouldAutoClose) return;

        _timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ComponentToken);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SnackbarInstance.Duration, _timeoutCts.Token);
                if (!IsDisposed && _isVisible)
                {
                    await InvokeAsync(CloseAsync);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when cancelled
            }
        }, _timeoutCts.Token);
    }

    /// <summary>
    ///     Executes an operation with retry logic.
    /// </summary>
    private async Task ExecuteWithRetry(Func<Task> operation, int maxRetries = MaxRetries)
    {
        var attempts = 0;
        while (attempts < maxRetries)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (ShouldRetry(ex, attempts))
            {
                attempts++;
                if (attempts < maxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempts));
                    await Task.Delay(delay, ComponentToken);
                    LogWarning("Retrying operation, attempt {Attempt}/{MaxRetries}", attempts, maxRetries);
                }
            }
        }
    }

    /// <summary>
    ///     Determines if an exception should trigger a retry.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldRetry(Exception ex, int currentAttempt) =>
        currentAttempt < MaxRetries && ex is JSException or TaskCanceledException;

    /// <summary>
    ///     Gets the CSS class for the snackbar type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetTypeClass() => SnackbarInstance.Type switch
    {
        SnackbarType.Success => "success",
        SnackbarType.Error => "error",
        SnackbarType.Warning => "warning",
        SnackbarType.Information => "info",
        _ => "info"
    };

    /// <summary>
    ///     Gets the CSS class for visibility state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetVisibilityClass() => _isVisible ? "visible" : "hidden";

    /// <summary>
    ///     Checks if an exception indicates element not found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsElementNotFoundError(JSException ex) =>
        ex.Message.Contains("not found") || ex.Message.Contains("null");

    #endregion
}
