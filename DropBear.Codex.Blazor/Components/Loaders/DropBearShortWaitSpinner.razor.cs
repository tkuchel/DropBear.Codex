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
///     A Blazor component for displaying a spinner during short wait times.
/// </summary>
public sealed partial class DropBearShortWaitSpinner : DropBearComponentBase
{
    #region Fields

    // Logger for this component.
    private new static readonly Microsoft.Extensions.Logging.ILogger Logger = CreateLogger();

    // Backing fields for parameters
    private string _title = "Please Wait";
    private string _message = "Processing your request";
    private string _spinnerSize = "md";
    private string _spinnerColor = "primary";

    // Flag to track if component should render
    private bool _shouldRender = true;

    // Cached aria label to avoid string concatenation on each render
    private string _cachedAriaLabel = string.Empty;

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
    protected override void OnInitialized()
    {
        base.OnInitialized();
        UpdateAriaLabel();
        LogSpinnerInitialized(Logger, _title, _message);
    }

    private static Microsoft.Extensions.Logging.ILogger CreateLogger()
    {
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Core.Logging.LoggerFactory.Logger.ForContext<DropBearShortWaitSpinner>());
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        return loggerFactory.CreateLogger(nameof(DropBearShortWaitSpinner));
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        UpdateAriaLabel();
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Gets the ARIA label for the spinner, combining the Title and Message.
    /// </summary>
    /// <returns>A string suitable for an ARIA label.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetSpinnerAriaLabel()
    {
        return _cachedAriaLabel;
    }

    /// <summary>
    ///     Updates the cached ARIA label when parameters change.
    /// </summary>
    private void UpdateAriaLabel()
    {
        _cachedAriaLabel = $"{_title}: {_message}";
    }

    #endregion

    #region Parameters

    /// <summary>
    ///     Gets or sets the title of the spinner.
    /// </summary>
    [Parameter]
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the message displayed with the spinner.
    /// </summary>
    [Parameter]
    public string Message
    {
        get => _message;
        set
        {
            if (_message != value)
            {
                _message = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the spinner size (e.g., "md").
    /// </summary>
    [Parameter]
    public string SpinnerSize
    {
        get => _spinnerSize;
        set
        {
            if (_spinnerSize != value)
            {
                _spinnerSize = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the spinner color (e.g., "primary").
    /// </summary>
    [Parameter]
    public string SpinnerColor
    {
        get => _spinnerColor;
        set
        {
            if (_spinnerColor != value)
            {
                _spinnerColor = value;
                _shouldRender = true;
            }
        }
    }

    #endregion

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Spinner initialized: Title='{Title}', Message='{Message}'")]
    static partial void LogSpinnerInitialized(Microsoft.Extensions.Logging.ILogger logger, string title, string message);

    #endregion
}
