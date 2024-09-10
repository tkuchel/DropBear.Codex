#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Manages modal components within the application.
/// </summary>
public sealed class ModalService : IModalService
{
    // Logger for ModalService
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ModalService>();

    // Queue to handle multiple modal requests.
    private readonly Queue<(Type componentType, IDictionary<string, object> parameters)> _modalQueue = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModalService" /> class.
    /// </summary>
    public ModalService()
    {
        CurrentComponent = null;
        CurrentParameters = null;
        IsModalVisible = false;
        Logger.Debug("ModalService initialized.");
    }

    // Properties to track the current modal component and its parameters.
    public Type? CurrentComponent { get; private set; }
    public IDictionary<string, object>? CurrentParameters { get; private set; }
    public bool IsModalVisible { get; private set; }

    // Event to notify the state change.
    public event Action? OnChange;

    /// <summary>
    ///     Shows the specified component as a modal with parameters.
    ///     Queues the modal if another one is already visible.
    /// </summary>
    /// <typeparam name="T">Component type to display.</typeparam>
    /// <param name="parameters">Optional parameters to pass to the component.</param>
    /// <returns>A Result object indicating the outcome of the operation.</returns>
    public Result Show<T>(IDictionary<string, object>? parameters = null) where T : DropBearComponentBase
    {
        parameters ??= new Dictionary<string, object>();

        Logger.Information("Show called for modal of type {ComponentType}.", typeof(T));

        if (IsModalVisible)
        {
            Logger.Debug("Modal of type {ComponentType} is enqueued as another modal is currently displayed.",
                typeof(T));
            EnqueueModal(typeof(T), parameters);
        }
        else
        {
            Logger.Debug("No modal is currently displayed, displaying modal of type {ComponentType}.", typeof(T));
            DisplayModal(typeof(T), parameters);
        }

        return Result.Success();
    }

    /// <summary>
    ///     Closes the current modal and displays the next one in the queue, if available.
    /// </summary>
    public void Close()
    {
        try
        {
            Logger.Debug("Closing the current modal.");
            ResetCurrentModal();
            ProcessNextInQueue();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while closing the modal: {ErrorMessage}", ex.Message);
        }
        finally
        {
            NotifyStateChanged();
        }
    }

    /// <summary>
    ///     Enqueues a modal to be displayed later.
    /// </summary>
    /// <param name="componentType">The type of the modal component.</param>
    /// <param name="parameters">Parameters to pass to the component.</param>
    private void EnqueueModal(Type componentType, IDictionary<string, object> parameters)
    {
        _modalQueue.Enqueue((componentType, parameters));
        Logger.Information("Modal of type {ComponentType} has been enqueued.", componentType);
    }

    /// <summary>
    ///     Displays a modal component with the provided parameters.
    /// </summary>
    /// <param name="componentType">The type of the modal component.</param>
    /// <param name="parameters">Parameters for the modal component.</param>
    private void DisplayModal(Type componentType, IDictionary<string, object> parameters)
    {
        try
        {
            CurrentComponent = componentType;
            CurrentParameters = parameters;
            IsModalVisible = true;

            Logger.Information("Displaying modal of type {ComponentType}.", componentType);
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to display modal of type {ComponentType}: {ErrorMessage}", componentType,
                ex.Message);
        }
    }

    /// <summary>
    ///     Processes the next modal in the queue if available.
    /// </summary>
    private void ProcessNextInQueue()
    {
        if (_modalQueue.Any())
        {
            var (nextComponentType, nextParameters) = _modalQueue.Dequeue();
            Logger.Debug("Displaying next modal from the queue of type {ComponentType}.", nextComponentType);
            DisplayModal(nextComponentType, nextParameters);
        }
        else
        {
            Logger.Debug("No modals left in the queue.");
        }
    }

    /// <summary>
    ///     Resets the current modal's state.
    /// </summary>
    private void ResetCurrentModal()
    {
        IsModalVisible = false;
        CurrentComponent = null;
        CurrentParameters = null;
        Logger.Debug("Current modal has been reset.");
    }

    /// <summary>
    ///     Notifies subscribers of a state change in the modal service.
    /// </summary>
    private void NotifyStateChanged()
    {
        try
        {
            OnChange?.Invoke();
            Logger.Debug("Modal state changed.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while notifying modal state change: {ErrorMessage}", ex.Message);
        }
    }
}
