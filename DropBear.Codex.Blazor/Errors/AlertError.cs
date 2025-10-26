#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents errors that can occur during alert operations.
/// </summary>
public sealed record AlertError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AlertError" /> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public AlertError(string message) : base(message)
    {
    }

    /// <summary>
    ///     Gets or sets the alert ID associated with this error, if applicable.
    /// </summary>
    public string? AlertId { get; init; }

    /// <summary>
    ///     Gets or sets the operation that was attempted when the error occurred.
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    ///     Creates a new <see cref="AlertError" /> with details about a failed alert creation.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A new AlertError instance.</returns>
    public static AlertError CreateFailed(string message)
    {
        return new AlertError(message) { Operation = "Create" };
    }

    /// <summary>
    ///     Creates a new <see cref="AlertError" /> with details about a failed alert update.
    /// </summary>
    /// <param name="alertId">The ID of the alert that failed to update.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A new AlertError instance.</returns>
    public static AlertError UpdateFailed(string alertId, string message)
    {
        return new AlertError(message) { AlertId = alertId, Operation = "Update" };
    }

    /// <summary>
    ///     Creates a new <see cref="AlertError" /> with details about a failed alert removal.
    /// </summary>
    /// <param name="alertId">The ID of the alert that failed to be removed.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A new AlertError instance.</returns>
    public static AlertError RemoveFailed(string alertId, string message)
    {
        return new AlertError(message) { AlertId = alertId, Operation = "Remove" };
    }

    /// <summary>
    ///     Creates a new <see cref="AlertError" /> with details about a service disposal failure.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A new AlertError instance.</returns>
    public static AlertError DisposalFailed(string message)
    {
        return new AlertError(message) { Operation = "Dispose" };
    }
}
