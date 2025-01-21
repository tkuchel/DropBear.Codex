#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A Blazor component for displaying a spinner during short wait times.
/// </summary>
public sealed partial class DropBearShortWaitSpinner : DropBearComponentBase
{
    protected override void OnInitialized()
    {
        base.OnInitialized();
        Logger.Debug("Spinner initialized: Title='{Title}', Message='{Message}'", Title, Message);
    }

    private string GetSpinnerAriaLabel()
    {
        return $"{Title}: {Message}";
    }

    #region Parameters

    [Parameter] public string Title { get; set; } = "Please Wait";
    [Parameter] public string Message { get; set; } = "Processing your request";
    [Parameter] public string SpinnerSize { get; set; } = "md";
    [Parameter] public string SpinnerColor { get; set; } = "primary";

    #endregion
}
