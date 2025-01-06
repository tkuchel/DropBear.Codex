#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A container that dynamically adjusts its width and can optionally center its content horizontally/vertically.
/// </summary>
public sealed partial class DropBearSectionContainer : DropBearComponentBase
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearSectionContainer>();

    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private DotNetObjectReference<DropBearSectionContainer>? _dotNetRef;
    private volatile bool _isDisposed;
    private bool _isJsInitialized;

    private string MaxWidthStyle { get; set; } = "100%"; // Default fallback

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
    public string? MaxWidth { get; set; }

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
    ///     Dynamically builds the CSS class for the container.
    /// </summary>
    private string ContainerClass => BuildContainerClass();

    /// <summary>
    ///     Disposes resources, including JS listeners, when the component is destroyed.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_isJsInitialized)
            {
                try
                {
                    var isConnected = await JsRuntime.InvokeAsync<bool>("eval", "typeof window !== 'undefined'");
                    if (isConnected)
                    {
                        await JsRuntime.InvokeVoidAsync("DropBearResizeManager.dispose");
                        Logger.Debug("Resize event listener disposed for DropBearSectionContainer.");
                    }
                }
                catch (JSDisconnectedException ex)
                {
                    Logger.Warning(ex, "Resize event listener was already disposed (disconnected).");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error during JS interop disposal in DropBearSectionContainer.");
                }
            }

            _dotNetRef?.Dispose();
        }
        finally
        {
            _initializationLock.Release();
            _initializationLock.Dispose();
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_isDisposed)
        {
            await _initializationLock.WaitAsync();
            try
            {
                if (_isDisposed)
                {
                    return;
                }

                await WaitForJsInitializationAsync("DropBearResizeManager");
                _dotNetRef = DotNetObjectReference.Create(this);
                await JsRuntime.InvokeVoidAsync("DropBearResizeManager.initialize", _dotNetRef);
                _isJsInitialized = true;
                Logger.Debug("Resize event listener registered for DropBearSectionContainer.");

                await SetMaxWidthBasedOnWindowSize();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during initialization in DropBearSectionContainer: {Message}", ex.Message);
            }
            finally
            {
                _initializationLock.Release();
            }
        }
    }

    /// <summary>
    ///     JS-invokable method that recalculates and sets the container's max width.
    /// </summary>
    [JSInvokable]
    public async Task SetMaxWidthBasedOnWindowSize()
    {
        try
        {
            var dimensions = await JsRuntime.InvokeAsync<WindowDimensions>("getWindowDimensions");
            var windowWidth = dimensions.Width;

            if (!string.IsNullOrEmpty(MaxWidth) && MaxWidth.EndsWith("%"))
            {
                if (double.TryParse(MaxWidth.TrimEnd('%'), out var percentage))
                {
                    var calculatedWidth = windowWidth * (percentage / 100);
                    MaxWidthStyle = $"{calculatedWidth}px";
                    Logger.Debug("Calculated MaxWidth from percentage: {MaxWidthStyle}", MaxWidthStyle);
                }
                else
                {
                    Logger.Error("Failed to parse MaxWidth as a percentage: {MaxWidth}", MaxWidth);
                    MaxWidthStyle = "100%";
                }
            }
            else
            {
                // Non-percentage (e.g. "800px"), use directly
                MaxWidthStyle = MaxWidth ?? "100%";
                Logger.Debug("MaxWidth set directly: {MaxWidthStyle}", MaxWidthStyle);
            }

            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error while setting max width in DropBearSectionContainer.");
            MaxWidthStyle = "100%";
        }
    }

    private string BuildContainerClass()
    {
        var classes = "section-container";
        if (IsHorizontalCentered)
        {
            classes += " horizontal-centered";
        }

        if (IsVerticalCentered)
        {
            classes += " vertical-centered";
        }

        return classes;
    }

    private async Task WaitForJsInitializationAsync(string objectName, int maxAttempts = 50)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            try
            {
                var isLoaded = await JsRuntime.InvokeAsync<bool>(
                    "eval",
                    $"typeof window.{objectName} !== 'undefined' && window.{objectName} !== null"
                );

                if (isLoaded)
                {
                    return;
                }

                await Task.Delay(100);
            }
            catch
            {
                await Task.Delay(100);
            }
        }

        throw new JSException($"JavaScript object {objectName} failed to initialize after {maxAttempts} attempts");
    }
}
