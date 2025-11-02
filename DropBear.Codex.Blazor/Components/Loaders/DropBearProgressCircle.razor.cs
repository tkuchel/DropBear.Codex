#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A circular progress indicator displaying a percentage from 0 to 100.
/// </summary>
public sealed partial class DropBearProgressCircle : DropBearComponentBase
{
    #region Methods

    /// <summary>
    ///     Validates the component parameters by clamping progress between 0 and 100
    ///     and ensuring that the size is a positive value.
    /// </summary>
    private void ValidateParameters()
    {
        var originalProgress = _progress;
        var clampedProgress = Math.Clamp(_progress, 0, 100);

        if (clampedProgress != originalProgress)
        {
            _progress = clampedProgress;
            LogProgressClamped(Logger, originalProgress, _progress);
            _shouldRender = true;
        }

        if (_size <= 0)
        {
            _size = DEFAULT_VIEWBOX_SIZE;
            LogInvalidSizeCorrected(Logger, _size);
            _shouldRender = true;
        }

        LogCircleParameters(Logger, _progress, _size);
    }

    private static Microsoft.Extensions.Logging.ILogger CreateLogger()
    {
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Core.Logging.LoggerFactory.Logger.ForContext<DropBearProgressCircle>());
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        return loggerFactory.CreateLogger(nameof(DropBearProgressCircle));
    }

    #endregion

    #region Fields and Constants

    // Logger for this component.
    private new static readonly Microsoft.Extensions.Logging.ILogger Logger = CreateLogger();

    private const int DEFAULT_VIEWBOX_SIZE = 60;
    private const int STROKE_WIDTH = 4;
    private const double TWO_PI = 2 * Math.PI;

    // Calculate radius and circumference based on default size and stroke width.
    private static readonly int Radius = (DEFAULT_VIEWBOX_SIZE / 2) - STROKE_WIDTH;
    private static readonly double Circumference = TWO_PI * Radius;

    // Backing fields for parameters
    private int _progress;
    private int _size = DEFAULT_VIEWBOX_SIZE;

    // Flag to track if component should render
    private bool _shouldRender = true;

    // Cached offset value to avoid recalculation

    /// <summary>
    ///     Gets the computed stroke offset based on the current progress.
    /// </summary>
    private double Offset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        set;
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Controls whether the component should render, optimizing for performance.
    /// </summary>
    /// <returns>True if the component should render, false otherwise.</returns>
    protected override bool ShouldRender()
    {
        if (_shouldRender)
        {
            _shouldRender = false;
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ValidateParameters();

        // Calculate offset only when parameters change
        Offset = Circumference - (_progress / 100.0 * Circumference);
    }

    #endregion

    #region Parameters

    /// <summary>
    ///     Gets or sets the progress value (0 to 100).
    /// </summary>
    [Parameter]
    public int Progress
    {
        get => _progress;
        set
        {
            if (_progress != value)
            {
                _progress = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the size of the viewbox. Defaults to 60.
    /// </summary>
    [Parameter]
    public int Size
    {
        get => _size;
        set
        {
            if (_size != value)
            {
                _size = value;
                _shouldRender = true;
            }
        }
    }

    #endregion

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Progress clamped: {Original} -> {Progress}")]
    static partial void LogProgressClamped(Microsoft.Extensions.Logging.ILogger logger, int original, int progress);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Invalid size value corrected to default: {Size}")]
    static partial void LogInvalidSizeCorrected(Microsoft.Extensions.Logging.ILogger logger, int size);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Circle parameters: Progress={Progress}, Size={Size}")]
    static partial void LogCircleParameters(Microsoft.Extensions.Logging.ILogger logger, int progress, int size);

    #endregion
}
