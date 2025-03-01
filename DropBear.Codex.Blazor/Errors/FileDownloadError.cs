#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents errors that occur during file download operations.
///     Provides common error patterns and factory methods for consistent error handling.
/// </summary>
public sealed record FileDownloadError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileDownloadError" /> class.
    /// </summary>
    /// <param name="message">The error message explaining the download failure.</param>
    public FileDownloadError(string message) : base(message) { }

    /// <summary>
    ///     Creates an error for when a network failure occurs during download.
    /// </summary>
    /// <param name="details">Details about the network failure.</param>
    /// <returns>A new <see cref="FileDownloadError" /> with appropriate message.</returns>
    public static FileDownloadError NetworkFailure(string details)
    {
        return new FileDownloadError($"Network failure: {details}");
    }

    /// <summary>
    ///     Creates an error for when access is denied to the file.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>A new <see cref="FileDownloadError" /> with appropriate message.</returns>
    public static FileDownloadError AccessDenied(string path)
    {
        return new FileDownloadError($"Access denied to file: {path}");
    }

    /// <summary>
    ///     Creates an error for when a file is not found.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>A new <see cref="FileDownloadError" /> with appropriate message.</returns>
    public static FileDownloadError FileNotFound(string path)
    {
        return new FileDownloadError($"File not found: {path}");
    }

    /// <summary>
    ///     Creates an error for when a download times out.
    /// </summary>
    /// <param name="durationMs">The timeout duration in milliseconds.</param>
    /// <returns>A new <see cref="FileDownloadError" /> with appropriate message.</returns>
    public static FileDownloadError Timeout(int durationMs)
    {
        return new FileDownloadError($"Download timed out after {durationMs}ms");
    }

    /// <summary>
    ///     Creates an error for when a download is cancelled by the user.
    /// </summary>
    /// <returns>A new <see cref="FileDownloadError" /> with appropriate message.</returns>
    public static FileDownloadError Cancelled()
    {
        return new FileDownloadError("Download was cancelled by the user");
    }

    /// <summary>
    ///     Creates an error for when an invalid state prevents download.
    /// </summary>
    /// <param name="details">Details about the invalid state.</param>
    /// <returns>A new <see cref="FileDownloadError" /> with appropriate message.</returns>
    public static FileDownloadError InvalidState(string details)
    {
        return new FileDownloadError($"Cannot download due to invalid state: {details}");
    }

    /// <summary>
    ///     Creates an error for when a JavaScript error occurs.
    /// </summary>
    /// <param name="details">Details about the JavaScript error.</param>
    /// <returns>A new <see cref="FileDownloadError" /> with appropriate message.</returns>
    public static FileDownloadError JavaScriptError(string details)
    {
        return new FileDownloadError($"JavaScript error during download: {details}");
    }

    /// <summary>
    ///     Creates an error for when a download fails for a specific reason.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="reason">The reason for the failure.</param>
    /// <returns>A new <see cref="FileDownloadError" /> with appropriate message.</returns>
    public static FileDownloadError DownloadFailed(string fileName, string reason)
    {
        return new FileDownloadError($"Failed to download file '{fileName}': {reason}");
    }

    /// <summary>
    ///     Creates an error for when a download operation fails.
    /// </summary>
    /// <param name="reason">The reason for the failure.</param>
    /// <returns>A new <see cref="FileDownloadError" /> with appropriate message.</returns>
    public static FileDownloadError OperationFailed(string reason)
    {
        return new FileDownloadError($"Download operation failed: {reason}");
    }

    /// <summary>
    ///     Creates an error for when an upload function is mistakenly called.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="reason">The reason for the failure.</param>
    /// <returns>A new <see cref="FileDownloadError" /> with appropriate message.</returns>
    public static FileDownloadError UploadFailed(string fileName, string reason)
    {
        return DownloadFailed(fileName, reason);
    }
}
