namespace DropBear.Codex.Files.Errors;

/// <summary>
///     Represents errors that occur during storage operations.
/// </summary>
public sealed record StorageError : FilesError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="StorageError" />.
    /// </summary>
    /// <param name="message">The error message describing the failure condition.</param>
    public StorageError(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Creates an error indicating that a read operation failed.
    /// </summary>
    /// <param name="path">The path that was being read.</param>
    /// <param name="message">A message describing why the read failed.</param>
    /// <returns>A <see cref="StorageError" /> with a descriptive message.</returns>
    public static StorageError ReadFailed(string path, string message)
    {
        return new StorageError($"Failed to read from {path}: {message}");
    }

    /// <summary>
    ///     Creates an error indicating that a write operation failed.
    /// </summary>
    /// <param name="path">The path that was being written to.</param>
    /// <param name="message">A message describing why the write failed.</param>
    /// <returns>A <see cref="StorageError" /> with a descriptive message.</returns>
    public static StorageError WriteFailed(string path, string message)
    {
        return new StorageError($"Failed to write to {path}: {message}");
    }

    /// <summary>
    ///     Creates an error indicating that a delete operation failed.
    /// </summary>
    /// <param name="path">The path that was being deleted.</param>
    /// <param name="message">A message describing why the delete failed.</param>
    /// <returns>A <see cref="StorageError" /> with a descriptive message.</returns>
    public static StorageError DeleteFailed(string path, string message)
    {
        return new StorageError($"Failed to delete {path}: {message}");
    }

    /// <summary>
    ///     Creates an error indicating that an update operation failed.
    /// </summary>
    /// <param name="path">The path that was being updated.</param>
    /// <param name="message">A message describing why the update failed.</param>
    /// <returns>A <see cref="StorageError" /> with a descriptive message.</returns>
    public static StorageError UpdateFailed(string path, string message)
    {
        return new StorageError($"Failed to update {path}: {message}");
    }

    /// <summary>
    ///     Creates an error indicating that input validation failed.
    /// </summary>
    /// <param name="paramName">The name of the invalid parameter.</param>
    /// <param name="reason">The reason why the input is invalid.</param>
    /// <returns>A <see cref="StorageError" /> with a descriptive message.</returns>
    public static StorageError InvalidInput(string paramName, string reason)
    {
        return new StorageError($"Invalid input for parameter '{paramName}': {reason}");
    }

    /// <summary>
    ///     Creates an error indicating that storage creation failed.
    /// </summary>
    /// <param name="message">A message describing why the creation failed.</param>
    /// <returns>A <see cref="StorageError" /> with a descriptive message.</returns>
    public static StorageError CreationFailed(string message)
    {
        return new StorageError($"Failed to create storage: {message}");
    }
}
