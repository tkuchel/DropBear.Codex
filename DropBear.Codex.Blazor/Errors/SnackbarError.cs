#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents errors that occur during snackbar operations.
/// </summary>
public sealed record SnackbarError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SnackbarError" /> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public SnackbarError(string message) : base(message) { }

    /// <summary>
    ///     Creates an error for when a snackbar cannot be found.
    /// </summary>
    /// <param name="id">The ID of the snackbar that was not found.</param>
    /// <returns>A new <see cref="SnackbarError" /> instance.</returns>
    public static SnackbarError NotFound(string id)
    {
        return new SnackbarError($"Snackbar with ID '{id}' was not found");
    }

    /// <summary>
    ///     Creates an error for when a limit is reached.
    /// </summary>
    /// <param name="details">Details about the limit.</param>
    /// <returns>A new <see cref="SnackbarError" /> instance.</returns>
    public static SnackbarError LimitReached(string details)
    {
        return new SnackbarError($"Snackbar limit reached: {details}");
    }

    /// <summary>
    ///     Creates an error for when an operation times out.
    /// </summary>
    /// <param name="operation">The operation that timed out.</param>
    /// <returns>A new <see cref="SnackbarError" /> instance.</returns>
    public static SnackbarError Timeout(string operation)
    {
        return new SnackbarError($"Operation '{operation}' timed out");
    }

    /// <summary>
    ///     Creates an error for when a general operation fails.
    /// </summary>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="details">Details about the failure.</param>
    /// <returns>A new <see cref="SnackbarError" /> instance.</returns>
    public static SnackbarError OperationFailed(string operation, string details)
    {
        return new SnackbarError($"Operation '{operation}' failed: {details}");
    }
}
