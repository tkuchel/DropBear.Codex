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
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ShortWaitSpinner>();

    /// <summary>
    ///     The title displayed above the spinner.
    /// </summary>
    [Parameter] public string Title { get; set; } = "Please Wait";

    /// <summary>
    ///     The message displayed below the spinner.
    /// </summary>
    [Parameter] public string Message { get; set; } = "Processing your request";

    /// <summary>
    ///     The size of the spinner (default is medium).
    /// </summary>
    [Parameter] public string SpinnerSize { get; set; } = "md";

    /// <summary>
    ///     The color of the spinner (default is theme-based).
    /// </summary>
    [Parameter] public string SpinnerColor { get; set; } = "primary";

    protected override void OnInitialized()
    {
        Logger.Information("ShortWaitSpinner initialized with title '{Title}' and message '{Message}'", Title, Message);
    }

    /// <summary>
    ///     Renders the spinner with appropriate accessibility attributes.
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private string GetSpinnerAriaLabel() => $"{Title}: {Message}";
}
