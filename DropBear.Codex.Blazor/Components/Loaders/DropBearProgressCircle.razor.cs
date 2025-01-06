#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A circular progress indicator displaying a percentage from 0 to 100.
/// </summary>
public sealed partial class DropBearProgressCircle : DropBearComponentBase
{
    private const int DefaultViewBoxSize = 60;
    private const int StrokeWidth = 4;

    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearProgressCircle>();
    private static readonly int Radius = (DefaultViewBoxSize / 2) - StrokeWidth;
    private static readonly double Circumference = 2 * Math.PI * Radius;

    /// <summary>
    ///     The progress percentage (0-100) to display.
    /// </summary>
    [Parameter]
    public int Progress { get; set; }

    /// <summary>
    ///     The overall diameter in px for the circle.
    /// </summary>
    [Parameter]
    public int Size { get; set; } = DefaultViewBoxSize;

    /// <summary>
    ///     Calculates the stroke-dashoffset needed to visually represent the given Progress.
    /// </summary>
    private double Offset => Circumference - (Progress / 100.0 * Circumference);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Clamp progress to [0..100]
        var originalProgress = Progress;
        Progress = Math.Clamp(Progress, 0, 100);

        if (Progress != originalProgress)
        {
            Logger.Warning("Progress value clamped to {Progress} from {Original}", Progress, originalProgress);
        }

        // Ensure Size is positive
        if (Size <= 0)
        {
            Logger.Error("Invalid size parameter: {Size}. Must be > 0.", Size);
            throw new ArgumentException("Size must be a positive integer greater than zero.");
        }

        Logger.Debug("DropBearProgressCircle parameters set: Progress={Progress}, Size={Size}",
            Progress, Size);
    }
}
