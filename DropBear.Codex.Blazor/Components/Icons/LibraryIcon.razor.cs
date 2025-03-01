#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Icons;

public partial class LibraryIcon : DropBearComponentBase
{
    private bool _loadAttempted;

    private string? _previousIconName;
    private string? _svgContent;

    [Parameter] public string IconName { get; set; } = string.Empty;
    [Parameter] public string? Title { get; set; }
    [Parameter] public int Size { get; set; } = 20;
    [Parameter] public string Color { get; set; } = "currentColor";
    [Parameter] public string CssClass { get; set; } = string.Empty;
    [Parameter] public bool Clickable { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    protected override void OnParametersSet()
    {
        if (!string.IsNullOrEmpty(IconName) && (_svgContent == null || _previousIconName != IconName))
        {
            _loadAttempted = true;
            var result = IconLibrary.GetIcon(IconName);
            _svgContent = result.IsSuccess ? result.Value : null;
            _previousIconName = IconName;
        }
    }
}
