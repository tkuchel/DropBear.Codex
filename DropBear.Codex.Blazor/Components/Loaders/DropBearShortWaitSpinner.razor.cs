#region
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
#endregion

namespace DropBear.Codex.Blazor.Components.Loaders
{
    /// <summary>
    /// A Blazor component for displaying a spinner during short wait times.
    /// </summary>
    public sealed partial class DropBearShortWaitSpinner : DropBearComponentBase
    {
        #region Fields

        // Logger for this component.
        private new static readonly Serilog.ILogger Logger = LoggerFactory.Logger.ForContext<DropBearShortWaitSpinner>();

        #endregion

        #region Lifecycle Methods

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            base.OnInitialized();
            Logger.Debug("Spinner initialized: Title='{Title}', Message='{Message}'", Title, Message);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the ARIA label for the spinner, combining the Title and Message.
        /// </summary>
        /// <returns>A string suitable for an ARIA label.</returns>
        private string GetSpinnerAriaLabel()
        {
            return $"{Title}: {Message}";
        }

        #endregion

        #region Parameters

        /// <summary>
        /// Gets or sets the title of the spinner.
        /// </summary>
        [Parameter] public string Title { get; set; } = "Please Wait";

        /// <summary>
        /// Gets or sets the message displayed with the spinner.
        /// </summary>
        [Parameter] public string Message { get; set; } = "Processing your request";

        /// <summary>
        /// Gets or sets the spinner size (e.g., "md").
        /// </summary>
        [Parameter] public string SpinnerSize { get; set; } = "md";

        /// <summary>
        /// Gets or sets the spinner color (e.g., "primary").
        /// </summary>
        [Parameter] public string SpinnerColor { get; set; } = "primary";

        #endregion
    }
}
