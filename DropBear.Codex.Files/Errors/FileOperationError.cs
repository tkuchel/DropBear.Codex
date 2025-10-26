#region

#endregion

namespace DropBear.Codex.Files.Errors;

/// <summary>
///     Represents errors that occur during file operations.
/// </summary>
public sealed record FileOperationError : FilesError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="FileOperationError" />.
    /// </summary>
    /// <param name="message">The error message describing the failure condition.</param>
    public FileOperationError(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Creates an error indicating that a file was not found.
    /// </summary>
    /// <param name="path">The path of the file that was not found.</param>
    /// <returns>A <see cref="FileOperationError" /> with a descriptive message.</returns>
    public static FileOperationError FileNotFound(string path)
    {
        return new FileOperationError($"File not found: {path}");
    }

    /// <summary>
    ///     Creates an error indicating that access to a file was denied.
    /// </summary>
    /// <param name="path">The path of the file that access was denied to.</param>
    /// <returns>A <see cref="FileOperationError" /> with a descriptive message.</returns>
    public static FileOperationError AccessDenied(string path)
    {
        return new FileOperationError($"Access denied to file: {path}");
    }

    /// <summary>
    ///     Creates an error indicating that an invalid operation was attempted.
    /// </summary>
    /// <param name="message">A message describing why the operation was invalid.</param>
    /// <returns>A <see cref="FileOperationError" /> with a descriptive message.</returns>
    public static FileOperationError InvalidOperation(string message)
    {
        return new FileOperationError(message);
    }

    /// <summary>
    ///     Creates an error indicating that a serialization operation failed.
    /// </summary>
    /// <param name="message">A message describing why the serialization failed.</param>
    /// <returns>A <see cref="FileOperationError" /> with a descriptive message.</returns>
    public static FileOperationError SerializationFailed(string message)
    {
        return new FileOperationError($"Serialization failed: {message}");
    }

    /// <summary>
    ///     Creates an error indicating that a read operation failed.
    /// </summary>
    /// <param name="path">The path that was being read.</param>
    /// <param name="message">A message describing why the read failed.</param>
    /// <returns>A <see cref="FileOperationError" /> with a descriptive message.</returns>
    public static FileOperationError ReadFailed(string path, string message)
    {
        return new FileOperationError($"Failed to read from {path}: {message}");
    }

    /// <summary>
    ///     Creates an error indicating that a write operation failed.
    /// </summary>
    /// <param name="path">The path that was being written to.</param>
    /// <param name="message">A message describing why the write failed.</param>
    /// <returns>A <see cref="FileOperationError" /> with a descriptive message.</returns>
    public static FileOperationError WriteFailed(string path, string message)
    {
        return new FileOperationError($"Failed to write to {path}: {message}");
    }

    /// <summary>
    ///     Creates an error indicating that a delete operation failed.
    /// </summary>
    /// <param name="path">The path that was being deleted.</param>
    /// <param name="message">A message describing why the delete failed.</param>
    /// <returns>A <see cref="FileOperationError" /> with a descriptive message.</returns>
    public static FileOperationError DeleteFailed(string path, string message)
    {
        return new FileOperationError($"Failed to delete {path}: {message}");
    }

    /// <summary>
    ///     Creates an error indicating that an update operation failed.
    /// </summary>
    /// <param name="path">The path that was being updated.</param>
    /// <param name="message">A message describing why the update failed.</param>
    /// <returns>A <see cref="FileOperationError" /> with a descriptive message.</returns>
    public static FileOperationError UpdateFailed(string path, string message)
    {
        return new FileOperationError($"Failed to update {path}: {message}");
    }
}
