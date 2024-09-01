#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A Blazor component for displaying a spinner for short wait times.
/// </summary>
public sealed partial class ShortWaitSpinner : DropBearComponentBase
{
    [Parameter] public string Title { get; set; } = "Short Wait";
    [Parameter] public string Message { get; set; } = "Please wait while we process your request.";

    // The method to get a theme-specific CSS class has been removed as it's no longer needed
}
