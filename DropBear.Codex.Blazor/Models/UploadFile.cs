#region

using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components.Forms;

#endregion

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
    /// <param name="droppedFileData">The byte array data for dropped files.</param>
    /// <param name="uploadStatus">The initial upload status of the file (optional).</param>
    /// <param name="uploadProgress">The initial upload progress percentage (optional, between 0 and 100).</param>
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
    ///     Gets or sets the browser file data (used for files from InputFile).
    /// </summary>
    public IBrowserFile? FileData { get; set; }

    /// <summary>
    ///     Gets or sets the dropped file data (used for dropped files as byte array).
    /// </summary>
    public byte[]? DroppedFileData { get; set; }
}
