#region

using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components.Forms;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a file that is to be uploaded, with support for both
///     <see cref="IBrowserFile" /> (from &lt;InputFile /&gt;) and raw byte array (dropped files).
/// </summary>
public sealed class UploadFile
{
    private int _uploadProgress;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UploadFile" /> class.
    /// </summary>
    /// <param name="name">The name of the file (including extension if any).</param>
    /// <param name="size">The file size in bytes.</param>
    /// <param name="contentType">The MIME type of the file (e.g., "image/png").</param>
    /// <param name="fileData">An optional <see cref="IBrowserFile" /> for files uploaded via &lt;InputFile /&gt;.</param>
    /// <param name="droppedFileData">An optional byte array for files dropped in a drag-and-drop scenario.</param>
    /// <param name="uploadStatus">
    ///     An initial <see cref="UploadStatus" />, default is
    ///     <see cref="DropBear.Codex.Blazor.Enums.UploadStatus.Ready" />.
    /// </param>
    /// <param name="uploadProgress">Initial progress (0-100). Defaults to 0.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="name" /> or <paramref name="contentType" /> is null/empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown if <paramref name="size" /> is negative or <paramref name="uploadProgress" /> is outside 0..100.
    /// </exception>
    public UploadFile(
        string name,
        long size,
        string contentType,
        IBrowserFile? fileData = null,
        byte[]? droppedFileData = null,
        UploadStatus uploadStatus = UploadStatus.Ready,
        int uploadProgress = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentNullException(nameof(name), "File name cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentNullException(nameof(contentType), "Content type cannot be null or empty.");
        }

        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "File size must be greater than or equal to 0.");
        }

        if (uploadProgress is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(uploadProgress), "Upload progress must be between 0 and 100.");
        }

        Name = name;
        Size = size;
        ContentType = contentType;
        FileData = fileData; // Used for <InputFile>
        DroppedFileData = droppedFileData; // Used for dropped files
        UploadStatus = uploadStatus;
        UploadProgress = uploadProgress;
    }

    /// <summary>
    ///     Gets the name of the file.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the size of the file in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    ///     Gets the MIME type of the file (e.g. "image/png").
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    ///     Gets or sets the status of the file upload (e.g. Ready, InProgress, Success, Failure).
    /// </summary>
    public UploadStatus UploadStatus { get; set; }

    /// <summary>
    ///     Gets or sets the upload progress (0-100).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown if the assigned value is outside 0..100.
    /// </exception>
    public int UploadProgress
    {
        get => _uploadProgress;
        set => _uploadProgress = value is >= 0 and <= 100
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "UploadProgress must be between 0 and 100.");
    }

    /// <summary>
    ///     Gets or sets the <see cref="IBrowserFile" /> data (used when uploaded from an &lt;InputFile /&gt;).
    /// </summary>
    public IBrowserFile? FileData { get; set; }

    /// <summary>
    ///     Gets or sets the raw byte array of the file (used when files are dragged and dropped).
    /// </summary>
    public byte[]? DroppedFileData { get; set; }
}
