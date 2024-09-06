#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Core;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Manages modal components within the application.
/// </summary>
public sealed class ModalService : IModalService
{
    // Queue to handle multiple modal requests.
    private readonly Queue<(Type componentType, IDictionary<string, object> parameters)> _modalQueue = new();

    public ModalService()
    {
        CurrentComponent = null;
        CurrentParameters = null;
        IsModalVisible = false;
    }

    // Properties to track the current modal component and its parameters.
    public Type? CurrentComponent { get; private set; }
    public IDictionary<string, object>? CurrentParameters { get; private set; }
    public bool IsModalVisible { get; private set; }

    // Event to notify the state change.
    public event Action? OnChange;

    /// <summary>
    ///     Show the specified component as a modal with parameters.
    /// </summary>
    /// <typeparam name="T">Component type to display.</typeparam>
    /// <param name="parameters">Optional parameters to pass to the component.</param>
    /// <returns>A Result object indicating the outcome of the operation.</returns>
    public Result Show<T>(IDictionary<string, object>? parameters = null) where T : DropBearComponentBase
    {
        parameters ??= new Dictionary<string, object>(); // Initialize parameters if null.

        if (IsModalVisible)
        {
            // Queue the modal if another one is already displayed.
            _modalQueue.Enqueue((typeof(T), parameters));
        }
        else
        {
            // Show the modal immediately if none is displayed.
            DisplayModal<T>(parameters);
        }

        return Result.Success();
    }

    /// <summary>
    ///     Close the current modal and show the next one in the queue, if available.
    /// </summary>
    public void Close()
    {
        IsModalVisible = false;
        CurrentComponent = null;
        CurrentParameters = null;

        if (_modalQueue.Count > 0)
        {
            var nextModal = _modalQueue.Dequeue();
            DisplayModal(nextModal.componentType, nextModal.parameters);
        }

        NotifyStateChanged();
    }

    /// <summary>
    ///     Notifies that the modal state has changed.
    /// </summary>
    private void NotifyStateChanged()
    {
        try
        {
            OnChange?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while notifying state change. Error: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    ///     Helper method to display a modal with the specified parameters.
    /// </summary>
    /// <typeparam name="T">Component type to display.</typeparam>
    /// <param name="parameters">Optional parameters for the component.</param>
    private void DisplayModal<T>(IDictionary<string, object> parameters) where T : DropBearComponentBase
    {
        try
        {
            CurrentComponent = typeof(T);
            CurrentParameters = parameters;
            IsModalVisible = true;

            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to display modal for component {ComponentType}. Error: {ErrorMessage}", typeof(T),
                ex.Message);
        }
    }

    /// <summary>
    ///     Overloaded method to display a modal without specifying a generic type.
    /// </summary>
    /// <param name="componentType">Type of the modal component.</param>
    /// <param name="parameters">Parameters to pass to the component.</param>
    private void DisplayModal(Type componentType, IDictionary<string, object> parameters)
    {
        try
        {
            CurrentComponent = componentType;
            CurrentParameters = parameters;
            IsModalVisible = true;

            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to display modal for component {ComponentType}. Error: {ErrorMessage}", componentType,
                ex.Message);
        }
    }
}
