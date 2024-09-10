#region

using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components.Forms;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a file to be uploaded.
/// </summary>
public sealed class UploadFile
{
    private int _uploadProgress;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UploadFile" /> class.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="size">The size of the file in bytes.</param>
    /// <param name="contentType">The MIME type of the file.</param>
    /// <param name="fileData">The browser file data.</param>
    /// <param name="uploadStatus">The initial upload status of the file (optional).</param>
    /// <param name="uploadProgress">The initial upload progress percentage (optional, between 0 and 100).</param>
    /// <exception cref="ArgumentNullException">Thrown when the file name or content type is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the file size or upload progress is invalid.</exception>
    public UploadFile(
        string name,
        long size,
        string contentType,
        IBrowserFile? fileData,
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
        FileData = fileData;
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
    ///     Gets the MIME type of the file.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    ///     Gets or sets the upload status of the file.
    /// </summary>
    public UploadStatus UploadStatus { get; set; }

    /// <summary>
    ///     Gets or sets the upload progress of the file as a percentage.
    /// </summary>
    public int UploadProgress
    {
        get => _uploadProgress;
        set => _uploadProgress = value is >= 0 and <= 100
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "UploadProgress must be between 0 and 100.");
    }

    /// <summary>
    ///     Gets or sets the browser file data.
    /// </summary>
    public IBrowserFile? FileData { get; set; }
}
