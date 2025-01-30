namespace DropBear.Codex.Blazor.Enums;

/// <summary>
/// Represents the current status of a file upload.
/// </summary>
public enum UploadStatus
{
    /// <summary>
    /// File is pending upload
    /// </summary>
    Pending,

    /// <summary>
    /// File is currently being uploaded
    /// </summary>
    Uploading,

    /// <summary>
    /// File was uploaded successfully
    /// </summary>
    Success,

    /// <summary>
    /// File upload failed
    /// </summary>
    Failure,

    /// <summary>
    /// File upload completed with warnings
    /// </summary>
    Warning,

    /// <summary>
    /// File upload was cancelled
    /// </summary>
    Cancelled
}
