#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Core;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

public sealed class ModalService : IModalService
{
    private readonly Queue<(Type componentType, IDictionary<string, object> parameters)> _modalQueue = new();

    public ModalService()
    {
        CurrentComponent = null;
        CurrentParameters = null;
        IsModalVisible = false;
    }

    public Type? CurrentComponent { get; private set; }
    public IDictionary<string, object>? CurrentParameters { get; private set; }
    public bool IsModalVisible { get; private set; }
    public event Action? OnChange;

    /// <summary>
    ///     Show the specified component as a modal with parameters.
    /// </summary>
    /// <typeparam name="T">Component type to show.</typeparam>
    /// <param name="parameters">Optional parameters to pass to the component.</param>
    /// <returns>A Result object indicating success or failure of the operation.</returns>
    public Result Show<T>(IDictionary<string, object>? parameters = null) where T : DropBearComponentBase
    {
        try
        {
            parameters ??= new Dictionary<string, object>(); // Ensure parameters is initialized

            if (IsModalVisible)
            {
                _modalQueue.Enqueue((typeof(T), parameters));
            }
            else
            {
                CurrentComponent = typeof(T);
                CurrentParameters = parameters;
                IsModalVisible = true;
                NotifyStateChanged();
            }

            return Result.Success();
        }
        catch (Exception e)
        {
            // Log detailed information
            Log.Error(e,
                "Error in Show method while attempting to display component {ComponentType}. Error: {ErrorMessage}",
                typeof(T), e.Message);
            return Result.Failure($"Failed to show the modal for component {typeof(T)}. Error: {e.Message}");
        }
    }

    /// <summary>
    ///     Close the current modal and show the next one in the queue, if any.
    /// </summary>
    public void Close()
    {
        try
        {
            IsModalVisible = false;
            CurrentComponent = null;
            CurrentParameters = null;

            if (_modalQueue.Count > 0)
            {
                var nextModal = _modalQueue.Dequeue();
                CurrentComponent = nextModal.componentType;
                CurrentParameters = nextModal.parameters;
                IsModalVisible = true;
            }

            NotifyStateChanged();
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred while attempting to close the modal. Error: {ErrorMessage}", e.Message);
        }
    }

    private void NotifyStateChanged()
    {
        try
        {
            OnChange?.Invoke();
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred while notifying state change. Error: {ErrorMessage}", e.Message);
        }
    }
}
