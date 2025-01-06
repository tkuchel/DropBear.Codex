#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Core.Results.Compatibility;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Manages the display of modals in the application, ensuring only one modal
///     is visible at a time and queuing others if necessary.
/// </summary>
public sealed class ModalService : IModalService
{
    private readonly object _lock = new();
    private readonly ILogger<ModalService> _logger;

    /// <summary>
    ///     Queue to hold modals if another one is currently displayed.
    /// </summary>
    private readonly Queue<(Type ComponentType, IDictionary<string, object> Parameters)> _modalQueue = new();

    private Type? _currentComponent;
    private IDictionary<string, object>? _currentParameters;
    private bool _isModalVisible;

    /// <summary>
    ///     Creates a new instance of the <see cref="ModalService" /> class.
    /// </summary>
    /// <param name="logger">A logger instance for logging debug information.</param>
    public ModalService(ILogger<ModalService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("ModalService initialized.");
    }

    /// <summary>
    ///     Gets the currently visible modal component type, or <c>null</c> if none.
    /// </summary>
    public Type? CurrentComponent
    {
        get
        {
            lock (_lock)
            {
                return _currentComponent;
            }
        }
        private set
        {
            lock (_lock)
            {
                _currentComponent = value;
            }
        }
    }

    /// <summary>
    ///     Gets the parameters for the current modal component, if any.
    /// </summary>
    public IDictionary<string, object>? CurrentParameters
    {
        get
        {
            lock (_lock)
            {
                return _currentParameters;
            }
        }
        private set
        {
            lock (_lock)
            {
                _currentParameters = value;
            }
        }
    }

    /// <summary>
    ///     Indicates whether a modal is currently visible.
    /// </summary>
    public bool IsModalVisible
    {
        get
        {
            lock (_lock)
            {
                return _isModalVisible;
            }
        }
        private set
        {
            lock (_lock)
            {
                _isModalVisible = value;
            }
        }
    }

    /// <summary>
    ///     Event fired whenever the modal state changes, allowing the UI to re-render.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    ///     Shows a modal of type <typeparamref name="T" />. If another modal is already visible,
    ///     the new modal is added to a queue and displayed later.
    /// </summary>
    /// <typeparam name="T">A Blazor component derived from <see cref="DropBearComponentBase" />.</typeparam>
    /// <param name="parameters">Optional parameters to pass to the modal component.</param>
    /// <returns>A <see cref="Result" /> indicating the outcome of the operation.</returns>
    public Result Show<T>(IDictionary<string, object>? parameters = null) where T : DropBearComponentBase
    {
        parameters ??= new Dictionary<string, object>();
        _logger.LogDebug("Show called for modal of type {ComponentType}.", typeof(T));

        var shouldNotify = false;

        lock (_lock)
        {
            if (_isModalVisible)
            {
                // Another modal is active; enqueue this one
                _logger.LogDebug("Modal of type {ComponentType} is enqueued (another is displayed).", typeof(T));
                _modalQueue.Enqueue((typeof(T), parameters));
            }
            else
            {
                // No active modal, display immediately
                _logger.LogDebug("No modal currently displayed; showing modal of type {ComponentType}.", typeof(T));
                _currentComponent = typeof(T);
                _currentParameters = parameters;
                _isModalVisible = true;
                shouldNotify = true;
            }
        }

        if (shouldNotify)
        {
            NotifyStateChanged();
        }

        return Result.Success();
    }

    /// <summary>
    ///     Closes the current modal and, if any modals are queued, displays the next one.
    /// </summary>
    public void Close()
    {
        bool shouldNotify;

        lock (_lock)
        {
            _logger.LogDebug("Closing the current modal.");
            _currentComponent = null;
            _currentParameters = null;
            _isModalVisible = false;

            // Check if we have a queued modal
            if (_modalQueue.TryDequeue(out var nextModal))
            {
                _logger.LogDebug("Displaying next modal from queue: type {ComponentType}.", nextModal.ComponentType);
                _currentComponent = nextModal.ComponentType;
                _currentParameters = nextModal.Parameters;
                _isModalVisible = true;
            }
            else
            {
                _logger.LogDebug("No modals left in the queue.");
            }

            shouldNotify = true;
        }

        if (shouldNotify)
        {
            NotifyStateChanged();
        }
    }

    /// <summary>
    ///     Invokes the <see cref="OnChange" /> event to notify any subscribers of the modal state update.
    /// </summary>
    private void NotifyStateChanged()
    {
        try
        {
            OnChange?.Invoke();
            _logger.LogDebug("Modal state changed notification triggered.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while notifying modal state change.");
        }
    }
}
