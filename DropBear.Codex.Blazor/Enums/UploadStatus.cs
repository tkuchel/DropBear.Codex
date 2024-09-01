namespace DropBear.Codex.Blazor.Enums;

/// <summary>
///     Defines the statuses that can be used for uploads.
/// </summary>
public enum UploadStatus
{
    /// <summary>
    ///     Ready to be uploaded.
    /// </summary>
    Ready,

    /// <summary>
    ///     Currently uploading.
    /// </summary>
    Uploading,

    /// <summary>
    ///     Successfully uploaded.
    /// </summary>
    Success,

    /// <summary>
    ///     Failed to upload.
    /// </summary>
    Failure,

    /// <summary>
    ///     Warning status for upload.
    /// </summary>
    Warning
}
