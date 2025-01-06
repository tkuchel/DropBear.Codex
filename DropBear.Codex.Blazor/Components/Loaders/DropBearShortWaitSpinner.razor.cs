#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A Blazor component for displaying a spinner during short wait times.
/// </summary>
public sealed partial class DropBearShortWaitSpinner : DropBearComponentBase
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearShortWaitSpinner>();

    /// <summary>
    ///     The title displayed above the spinner.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = "Please Wait";

    /// <summary>
    ///     The message displayed below the spinner.
    /// </summary>
    [Parameter]
    public string Message { get; set; } = "Processing your request";

    /// <summary>
    ///     The size of the spinner, e.g., "sm", "md", or "lg". Default is "md".
    /// </summary>
    [Parameter]
    public string SpinnerSize { get; set; } = "md";

    /// <summary>
    ///     The color of the spinner, e.g., "primary", "secondary". Default is "primary".
    /// </summary>
    [Parameter]
    public string SpinnerColor { get; set; } = "primary";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        Logger.Debug("DropBearShortWaitSpinner initialized. Title='{Title}', Message='{Message}'", Title, Message);
    }

    /// <summary>
    ///     Returns an ARIA label describing the spinner, for accessibility.
    /// </summary>
    private string GetSpinnerAriaLabel()
    {
        return $"{Title}: {Message}";
    }
}
