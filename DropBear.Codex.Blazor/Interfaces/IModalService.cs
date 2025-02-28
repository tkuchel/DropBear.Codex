#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Interface defining the contract for a modal service responsible for displaying modals
///     and managing their lifecycle, including queuing and parameter passing.
/// </summary>
public interface IModalService
{
    /// <summary>
    ///     The current component being displayed in the modal, if any.
    /// </summary>
    Type? CurrentComponent { get; }

    /// <summary>
    ///     The parameters associated with the current component being displayed in the modal.
    /// </summary>
    IDictionary<string, object>? CurrentParameters { get; }

    /// <summary>
    ///     Indicates whether a modal is currently visible.
    /// </summary>
    bool IsModalVisible { get; }

    /// <summary>
    ///     Gets the number of modals currently in the queue.
    /// </summary>
    int QueueCount { get; }

    /// <summary>
    ///     Event triggered when the modal state changes, used to notify subscribers.
    /// </summary>
    event Action? OnChange;

    /// <summary>
    ///     Displays the specified component as a modal, with optional parameters.
    ///     If another modal is already visible, the new modal will be added to a queue and displayed
    ///     when the current modal is closed.
    /// </summary>
    /// <typeparam name="T">The type of the component to display. Must inherit from <see cref="DropBearComponentBase" />.</typeparam>
    /// <param name="parameters">Optional parameters to pass to the component.</param>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    Result<Unit, ModalError> Show<T>(IDictionary<string, object>? parameters = null) where T : DropBearComponentBase;

    /// <summary>
    ///     Displays the specified component as a modal, with single parameter.
    ///     Convenience method for simple cases.
    /// </summary>
    /// <typeparam name="T">The type of the component to display. Must inherit from <see cref="DropBearComponentBase" />.</typeparam>
    /// <param name="parameterName">Name of the parameter to pass.</param>
    /// <param name="parameterValue">Value of the parameter to pass.</param>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    Result<Unit, ModalError> Show<T>(string parameterName, object parameterValue) where T : DropBearComponentBase;

    /// <summary>
    ///     Closes the currently displayed modal and displays the next modal in the queue, if any.
    /// </summary>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    Result<Unit, ModalError> Close();

    /// <summary>
    ///     Clears all modals from the queue and closes any currently displayed modal.
    /// </summary>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    Result<Unit, ModalError> ClearAll();
}
