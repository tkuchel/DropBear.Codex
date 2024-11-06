#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Compatibility;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Manages modal components within the application.
/// </summary>
public sealed class ModalService : IModalService
{
    private readonly object _lock = new();
    private readonly ILogger<ModalService> _logger;
    private readonly Queue<(Type ComponentType, IDictionary<string, object> Parameters)> _modalQueue = new();

    private Type? _currentComponent;
    private IDictionary<string, object>? _currentParameters;
    private bool _isModalVisible;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModalService" /> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ModalService(ILogger<ModalService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("ModalService initialized.");
    }

    /// <summary>
    ///     Gets the current modal component type.
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
    ///     Gets the parameters for the current modal component.
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
    ///     Gets a value indicating whether a modal is currently visible.
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
    ///     Occurs when the modal state changes.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    ///     Shows the specified component as a modal with parameters.
    ///     Queues the modal if another one is already visible.
    /// </summary>
    /// <typeparam name="T">Component type to display.</typeparam>
    /// <param name="parameters">Optional parameters to pass to the component.</param>
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
                _logger.LogDebug("Modal of type {ComponentType} is enqueued as another modal is currently displayed.",
                    typeof(T));
                _modalQueue.Enqueue((typeof(T), parameters));
            }
            else
            {
                _logger.LogDebug("No modal is currently displayed, displaying modal of type {ComponentType}.",
                    typeof(T));
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
    ///     Closes the current modal and displays the next one in the queue, if available.
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

            if (_modalQueue.TryDequeue(out var nextModal))
            {
                _logger.LogDebug("Displaying next modal from the queue of type {ComponentType}.",
                    nextModal.ComponentType);
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
    ///     Notifies subscribers of a state change in the modal service.
    /// </summary>
    private void NotifyStateChanged()
    {
        try
        {
            OnChange?.Invoke();
            _logger.LogDebug("Modal state changed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while notifying modal state change.");
        }
    }
}
