#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A Blazor component for displaying a spinner for short wait times.
/// </summary>
public sealed partial class ShortWaitSpinner : DropBearComponentBase
{
    /// <summary>
    ///     Gets or sets the logger instance.
    /// </summary>
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ShortWaitSpinner>();

    /// <summary>
    ///     Gets or sets the title displayed above the spinner.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = "Please Wait";

    /// <summary>
    ///     Gets or sets the message displayed below the spinner.
    /// </summary>
    [Parameter]
    public string Message { get; set; } = "Processing your request";

    /// <summary>
    ///     Gets or sets the size of the spinner (e.g., "sm", "md", "lg"). Default is "md".
    /// </summary>
    [Parameter]
    public string SpinnerSize { get; set; } = "md";

    /// <summary>
    ///     Gets or sets the color of the spinner. Default is "primary".
    /// </summary>
    [Parameter]
    public string SpinnerColor { get; set; } = "primary";

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Logger.Debug("ShortWaitSpinner initialized with title '{Title}' and message '{Message}'", Title, Message);
    }

    /// <summary>
    ///     Generates the ARIA label for the spinner for accessibility.
    /// </summary>
    /// <returns>The ARIA label string.</returns>
    private string GetSpinnerAriaLabel()
    {
        return $"{Title}: {Message}";
    }
}
