#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A circular progress indicator displaying a percentage from 0 to 100.
/// </summary>
public sealed partial class DropBearProgressCircle : DropBearComponentBase
{
    private const int DEFAULT_VIEWBOX_SIZE = 60;
    private const int STROKE_WIDTH = 4;
    private const double TWO_PI = 2 * Math.PI;

    private static readonly int Radius = (DEFAULT_VIEWBOX_SIZE / 2) - STROKE_WIDTH;
    private static readonly double Circumference = TWO_PI * Radius;

    private double Offset => Circumference - (Progress / 100.0 * Circumference);

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ValidateParameters();
    }

    private void ValidateParameters()
    {
        var originalProgress = Progress;
        Progress = Math.Clamp(Progress, 0, 100);

        if (Progress != originalProgress)
        {
            Logger.Warning("Progress clamped: {Original} -> {Progress}", originalProgress, Progress);
        }

        if (Size <= 0)
        {
            throw new ArgumentException("Size must be positive", nameof(Size));
        }

        Logger.Debug("Circle parameters: Progress={Progress}, Size={Size}", Progress, Size);
    }

    #region Parameters

    [Parameter] public int Progress { get; set; }
    [Parameter] public int Size { get; set; } = DEFAULT_VIEWBOX_SIZE;

    #endregion
}
