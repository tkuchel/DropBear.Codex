// CloseIcon.razor.cs
#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Icons;

/// <summary>
///     Component for rendering a close/dismiss icon with enhanced accessibility and customization.
/// </summary>
public partial class CloseIcon : DropBearComponentBase
{
    /// <summary>
    ///     Event callback that is triggered when the close icon is clicked.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>
    ///     Handles the click event on the close icon.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleCloseClick()
    {
        if (OnClose.HasDelegate)
        {
            await OnClose.InvokeAsync();
        }
    }
}
