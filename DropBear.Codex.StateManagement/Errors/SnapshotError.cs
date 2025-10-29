#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.StateManagement.Errors;

/// <summary>
///     Represents errors that can occur during snapshot operations.
///     Provides strongly-typed error information for the Result pattern.
/// </summary>
public sealed record SnapshotError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SnapshotError" /> class.
    /// </summary>
    /// <param name="message">The error message describing the snapshot failure.</param>
    public SnapshotError(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Gets or sets a flag indicating whether the error is transient and can be retried.
    /// </summary>
    public bool IsTransient { get; init; }

    /// <summary>
    ///     Creates a new <see cref="SnapshotError" /> from an exception.
    /// </summary>
    /// <param name="ex">The exception to create the error from.</param>
    /// <param name="isTransient">Whether the error is transient and can be retried.</param>
    /// <returns>A new <see cref="SnapshotError" /> instance.</returns>
    public static SnapshotError FromException(Exception ex, bool isTransient = false)
    {
        var error = new SnapshotError(ex.Message)
        {
            IsTransient = isTransient
        };

        // Use WithException to set the exception properly
        var errorWithException = (SnapshotError)error.WithException(ex);

        // Add additional metadata
        return (SnapshotError)errorWithException
            .WithMetadata("ExceptionType", ex.GetType().Name)
            .WithMetadata("IsTransient", isTransient);
    }

    /// <summary>
    ///     Creates a new <see cref="SnapshotError" /> with the specified context.
    /// </summary>
    /// <param name="context">The context where the error occurred.</param>
    /// <returns>A new <see cref="SnapshotError" /> with updated context.</returns>
    public SnapshotError WithContext(string context)
    {
        return (SnapshotError)WithMetadata("Context", context);
    }

    /// <summary>
    ///     Creates a new <see cref="SnapshotError" /> with the transient flag set.
    /// </summary>
    /// <param name="isTransient">Whether the error is transient.</param>
    /// <returns>A new <see cref="SnapshotError" /> with updated transient flag.</returns>
    public SnapshotError WithTransient(bool isTransient)
    {
        return this with { IsTransient = isTransient };
    }

    #region Factory Methods

    /// <summary>
    ///     Creates an error for when a snapshot is not found.
    /// </summary>
    public static SnapshotError NotFound(int version) =>
        new($"Snapshot version '{version}' was not found.");

    /// <summary>
    ///     Creates an error for when snapshotting is skipped due to interval constraints.
    /// </summary>
    public static SnapshotError IntervalNotReached() =>
        new("Snapshotting skipped due to snapshot interval not being reached.")
        {
            IsTransient = true
        };

    /// <summary>
    ///     Creates an error for when no current state is available.
    /// </summary>
    public static SnapshotError NoCurrentState() =>
        new("No current state available.");

    /// <summary>
    ///     Creates an error for when snapshot creation fails.
    /// </summary>
    public static SnapshotError CreationFailed(string reason) =>
        new($"Failed to create snapshot: {reason}")
        {
            IsTransient = true
        };

    /// <summary>
    ///     Creates an error for when snapshot restoration fails.
    /// </summary>
    public static SnapshotError RestorationFailed(string reason) =>
        new($"Failed to restore snapshot: {reason}");

    #endregion
}
