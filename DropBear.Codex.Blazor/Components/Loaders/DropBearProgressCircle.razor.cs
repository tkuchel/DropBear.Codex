#region
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
#endregion

namespace DropBear.Codex.Blazor.Components.Loaders
{
    /// <summary>
    /// A circular progress indicator displaying a percentage from 0 to 100.
    /// </summary>
    public sealed partial class DropBearProgressCircle : DropBearComponentBase
    {
        #region Fields and Constants

        // Logger for this component.
        private new static readonly Serilog.ILogger Logger = LoggerFactory.Logger.ForContext<DropBearProgressCircle>();

        private const int DEFAULT_VIEWBOX_SIZE = 60;
        private const int STROKE_WIDTH = 4;
        private const double TWO_PI = 2 * Math.PI;

        // Calculate radius and circumference based on default size and stroke width.
        private static readonly int Radius = (DEFAULT_VIEWBOX_SIZE / 2) - STROKE_WIDTH;
        private static readonly double Circumference = TWO_PI * Radius;

        /// <summary>
        /// Gets the computed stroke offset based on the current progress.
        /// </summary>
        private double Offset => Circumference - (Progress / 100.0 * Circumference);

        #endregion

        #region Lifecycle Methods

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            ValidateParameters();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Validates the component parameters by clamping progress between 0 and 100
        /// and ensuring that the size is a positive value.
        /// </summary>
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

        #endregion

        #region Parameters

        /// <summary>
        /// Gets or sets the progress value (0 to 100).
        /// </summary>
        [Parameter] public int Progress { get; set; }

        /// <summary>
        /// Gets or sets the size of the viewbox. Defaults to 60.
        /// </summary>
        [Parameter] public int Size { get; set; } = DEFAULT_VIEWBOX_SIZE;

        #endregion
    }
}
