#region

using System.Text;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A container that dynamically adjusts its width and can optionally center its content horizontally/vertically.
/// </summary>
public sealed partial class DropBearSectionContainer : DropBearComponentBase
{
    private const string DEFAULT_MAX_WIDTH = "100%";
    private const int RETRY_DELAY_MS = 100;
    private const int MAX_RETRIES = 3;
    private readonly CancellationTokenSource _disposalTokenSource = new();

    private readonly AsyncLock _initializationLock = new();
    private WindowDimensions? _cachedDimensions;
    private string? _containerClass;
    private DotNetObjectReference<DropBearSectionContainer>? _dotNetRef;
    private string? _maxWidth;
    private string _maxWidthStyle = DEFAULT_MAX_WIDTH;

    /// <summary>
    ///     Gets the CSS class for the container with caching.
    /// </summary>
    private string ContainerClass => _containerClass ??= BuildContainerClass();

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || IsDisposed)
        {
            return;
        }

        // Use your existing async lock to prevent multiple concurrent initializations
        using (await _initializationLock.LockAsync(_disposalTokenSource.Token))
        {
            if (IsDisposed)
            {
                return;
            }

            try
            {
                // Create DotNetObjectReference only if not already created
                _dotNetRef ??= DotNetObjectReference.Create(this);

                // 1) Ensure the 'DropBearResizeManager' module is fully registered in JS
                //    This will wait until 'window.DropBearResizeManager' is guaranteed to exist.
                await EnsureJsModuleInitializedAsync("DropBearResizeManager");

                // 2) Now attempt to create the manager with retry logic
                var retryCount = 0;
                while (retryCount < MAX_RETRIES)
                {
                    try
                    {
                        await SafeJsVoidInteropAsync("DropBearResizeManager.createResizeManager", _dotNetRef);

                        // Optionally call a method after creation
                        await SetMaxWidthBasedOnWindowSize();
                        break;
                    }
                    catch (Exception ex) when (retryCount < MAX_RETRIES - 1)
                    {
                        Logger.Warning(ex, "Retry {Count} initializing resize manager", retryCount + 1);
                        await Task.Delay(RETRY_DELAY_MS);
                        retryCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during container initialization");
                // Optionally surface this error to the UI or handle as needed
            }
        }
    }


    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _containerClass = null; // Invalidate cache
        base.OnParametersSet();
    }

    /// <summary>
    ///     JS-invokable method that recalculates and sets the container's max width.
    /// </summary>
    [JSInvokable]
    public async Task SetMaxWidthBasedOnWindowSize()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            var dimensions = await GetWindowDimensionsAsync();
            if (dimensions is null)
            {
                return;
            }

            var newMaxWidth = CalculateMaxWidth(dimensions.Width);
            if (_maxWidthStyle != newMaxWidth)
            {
                _maxWidthStyle = newMaxWidth;
                await InvokeStateHasChangedAsync(async () =>
                {
                    await NotifyDimensionsChangedAsync(dimensions);
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error setting max width");
            _maxWidthStyle = DEFAULT_MAX_WIDTH;
        }
    }

    /// <summary>
    ///     Gets the window dimensions with error handling.
    /// </summary>
    private async Task<WindowDimensions?> GetWindowDimensionsAsync()
    {
        try
        {
            await EnsureJsModuleInitializedAsync("DropBearResizeManager");
            var dimensions = await SafeJsInteropAsync<WindowDimensions>("getWindowDimensions");
            _cachedDimensions = dimensions;
            return dimensions;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to get window dimensions");
            return _cachedDimensions;
        }
    }

    /// <summary>
    ///     Builds the container CSS class.
    /// </summary>
    private string BuildContainerClass()
    {
        var builder = new StringBuilder("section-container", 100);

        if (IsHorizontalCentered)
        {
            builder.Append(" horizontal-centered");
        }

        if (IsVerticalCentered)
        {
            builder.Append(" vertical-centered");
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Calculates the max width based on window dimensions.
    /// </summary>
    private string CalculateMaxWidth(double windowWidth)
    {
        if (string.IsNullOrEmpty(MaxWidth))
        {
            return DEFAULT_MAX_WIDTH;
        }

        if (MaxWidth.EndsWith("%"))
        {
            if (double.TryParse(MaxWidth.TrimEnd('%'), out var percentage))
            {
                var calculatedWidth = windowWidth * (percentage / 100);
                return $"{calculatedWidth:F0}px";
            }

            Logger.Warning("Failed to parse MaxWidth percentage: {MaxWidth}", MaxWidth);
            return DEFAULT_MAX_WIDTH;
        }

        return MaxWidth; // Direct use for pixel values
    }

    /// <summary>
    ///     Notifies listeners of dimension changes.
    /// </summary>
    private async Task NotifyDimensionsChangedAsync(WindowDimensions dimensions)
    {
        if (!OnDimensionsChanged.HasDelegate)
        {
            return;
        }

        try
        {
            await OnDimensionsChanged.InvokeAsync(dimensions);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error notifying dimension change");
        }
    }

    /// <inheritdoc />
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            await EnsureJsModuleInitializedAsync("DropBearResizeManager");
            await SafeJsVoidInteropAsync("DropBearResizeManager.dispose");
            Logger.Debug("Resize manager disposed");
        }
        catch (ObjectDisposedException objectDisposedException)
        {
            Logger.Warning(objectDisposedException, "Error disposing JS resources, object already disposed");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error cleaning up JS resources");
        }
        finally
        {
            _dotNetRef?.Dispose();
            _initializationLock.Dispose();
            _disposalTokenSource.Dispose();
        }
    }

    #region Parameters

    /// <summary>
    ///     The content rendered within the container.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     The maximum width of the container, e.g., "800px" or "70%".
    /// </summary>
    [Parameter]
    public string? MaxWidth
    {
        get => _maxWidth;
        set
        {
            if (value != null && !value.EndsWith("%") && !value.EndsWith("px"))
            {
                throw new ArgumentException("MaxWidth must end with % or px", nameof(MaxWidth));
            }

            _maxWidth = value;
        }
    }

    /// <summary>
    ///     If true, horizontally center the container inside its parent.
    /// </summary>
    [Parameter]
    public bool IsHorizontalCentered { get; set; }

    /// <summary>
    ///     If true, vertically center the container inside its parent.
    /// </summary>
    [Parameter]
    public bool IsVerticalCentered { get; set; }

    /// <summary>
    ///     Event callback for when the container dimensions change.
    /// </summary>
    [Parameter]
    public EventCallback<WindowDimensions> OnDimensionsChanged { get; set; }

    #endregion
}
