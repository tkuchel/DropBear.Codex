#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A circular progress indicator.
/// </summary>
public sealed partial class ProgressCircle : DropBearComponentBase
{
    private const int DefaultViewBoxSize = 60;
    private const int StrokeWidth = 4;
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ProgressCircle>();
    private static readonly int Radius = (DefaultViewBoxSize / 2) - StrokeWidth;
    private static readonly double Circumference = 2 * Math.PI * Radius;

    /// <summary>
    ///     The progress percentage (0-100) to be displayed.
    /// </summary>
    [Parameter]
    public int Progress { get; set; }

    /// <summary>
    ///     The size of the circular progress indicator.
    /// </summary>
    [Parameter]
    public int Size { get; set; } = DefaultViewBoxSize;

    /// <summary>
    ///     The offset used to calculate the visible portion of the progress circle.
    /// </summary>
    private double Offset => Circumference - (Progress / 100.0 * Circumference);

    protected override void OnParametersSet()
    {
        // Ensure Progress is between 0 and 100
        var originalProgress = Progress;
        Progress = Math.Clamp(Progress, 0, 100);

        if (Progress != originalProgress)
        {
            Logger.Warning("Progress value clamped to {Progress} from {OriginalProgress}", Progress, originalProgress);
        }

        // Ensure Size is a positive value
        if (Size <= 0)
        {
            Logger.Error("Invalid size parameter {Size}. Size must be greater than zero.", Size);
            throw new ArgumentException("Size must be a positive value greater than zero.");
        }

        Logger.Debug("ProgressCircle parameters set: Progress = {Progress}, Size = {Size}", Progress, Size);
    }
}
