#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents errors that occur during file upload operations.
///     Provides common error patterns and factory methods for consistent error handling.
/// </summary>
public sealed record FileUploadError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileUploadError" /> class.
    /// </summary>
    /// <param name="message">The error message explaining the upload failure.</param>
    public FileUploadError(string message) : base(message) { }

    /// <summary>
    ///     Creates an error for when a file size exceeds the maximum allowed limit.
    /// </summary>
    /// <param name="size">The actual file size in bytes.</param>
    /// <param name="limit">The maximum allowed size in bytes.</param>
    /// <returns>A new <see cref="FileUploadError" /> with appropriate message.</returns>
    public static FileUploadError SizeLimitExceeded(long size, long limit)
    {
        return new FileUploadError($"File size {size} bytes exceeds limit of {limit} bytes");
    }

    /// <summary>
    ///     Creates an error for when a file type is not allowed.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="contentType">The MIME type of the file.</param>
    /// <returns>A new <see cref="FileUploadError" /> with appropriate message.</returns>
    public static FileUploadError InvalidFileType(string fileName, string contentType)
    {
        return new FileUploadError($"File '{fileName}' with type '{contentType}' is not allowed");
    }

    /// <summary>
    ///     Creates an error for when a file upload fails for a specific reason.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="reason">The reason for the failure.</param>
    /// <returns>A new <see cref="FileUploadError" /> with appropriate message.</returns>
    public static FileUploadError UploadFailed(string fileName, string reason)
    {
        return new FileUploadError($"Failed to upload file '{fileName}': {reason}");
    }

    /// <summary>
    ///     Creates an error for when a file has a blocked extension.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="extension">The file extension.</param>
    /// <returns>A new <see cref="FileUploadError" /> with appropriate message.</returns>
    public static FileUploadError BlockedExtension(string fileName, string extension)
    {
        return new FileUploadError($"File '{fileName}' has blocked extension '{extension}'");
    }

    /// <summary>
    ///     Creates an error for when an upload is cancelled.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>A new <see cref="FileUploadError" /> with appropriate message.</returns>
    public static FileUploadError Cancelled(string fileName)
    {
        return new FileUploadError($"Upload of file '{fileName}' was cancelled");
    }

    /// <summary>
    ///     Creates an error for when a circuit disconnection occurs during upload.
    /// </summary>
    /// <returns>A new <see cref="FileUploadError" /> with appropriate message.</returns>
    public static FileUploadError CircuitDisconnected()
    {
        return new FileUploadError("The upload was interrupted due to a connection loss");
    }

    /// <summary>
    ///     Creates an error for a batch upload failure.
    /// </summary>
    /// <param name="failedCount">The number of files that failed.</param>
    /// <param name="totalCount">The total number of files in the batch.</param>
    /// <returns>A new <see cref="FileUploadError" /> with appropriate message.</returns>
    public static FileUploadError BatchUploadPartialFailure(int failedCount, int totalCount)
    {
        return new FileUploadError($"{failedCount} of {totalCount} files failed to upload");
    }

    /// <summary>
    ///     Creates an error for when the component is in an invalid state for uploading.
    /// </summary>
    /// <param name="details">Details about the invalid state.</param>
    /// <returns>A new <see cref="FileUploadError" /> with appropriate message.</returns>
    public static FileUploadError InvalidState(string details)
    {
        return new FileUploadError($"Cannot upload due to invalid state: {details}");
    }
}
