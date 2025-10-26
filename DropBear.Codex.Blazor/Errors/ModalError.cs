#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents errors that can occur during modal operations.
/// </summary>
public sealed record ModalError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ModalError" /> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ModalError(string message) : base(message)
    {
    }

    /// <summary>
    ///     Gets or sets the modal component type associated with this error, if applicable.
    /// </summary>
    public string? ComponentType { get; init; }

    /// <summary>
    ///     Gets or sets the operation that was attempted when the error occurred.
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    ///     Creates a new <see cref="ModalError" /> with details about a failed modal show operation.
    /// </summary>
    /// <param name="componentType">The type of component that failed to show.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A new ModalError instance.</returns>
    public static ModalError ShowFailed(string componentType, string message)
    {
        return new ModalError(message) { ComponentType = componentType, Operation = "Show" };
    }

    /// <summary>
    ///     Creates a new <see cref="ModalError" /> with details about a failed modal close operation.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A new ModalError instance.</returns>
    public static ModalError CloseFailed(string message)
    {
        return new ModalError(message) { Operation = "Close" };
    }

    /// <summary>
    ///     Creates a new <see cref="ModalError" /> for queue-related issues.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A new ModalError instance.</returns>
    public static ModalError QueueFailed(string message)
    {
        return new ModalError(message) { Operation = "Queue" };
    }
}
