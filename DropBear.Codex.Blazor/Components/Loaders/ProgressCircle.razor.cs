#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

public sealed partial class ProgressCircle : DropBearComponentBase
{
    private const int DefaultViewBoxSize = 60;
    private const int StrokeWidth = 4;
    private static readonly int Radius = (DefaultViewBoxSize / 2) - StrokeWidth;
    private static readonly double Circumference = 2 * Math.PI * Radius;

    [Parameter] public int Progress { get; set; }
    [Parameter] public int Size { get; set; } = DefaultViewBoxSize;

    private double Offset => Circumference - (Progress / 100.0 * Circumference);

    protected override void OnParametersSet()
    {
        // Ensure Progress is between 0 and 100
        Progress = Math.Clamp(Progress, 0, 100);
    }
}
