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

    public UploadFile()
    {
        // Intentionally left blank
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="UploadFile" /> class.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="size">The size of the file in bytes.</param>
    /// <param name="contentType">The MIME type of the file.</param>
    /// <param name="fileData">The browser file data.</param>
    public UploadFile(string name, long size, string contentType, IBrowserFile? fileData)
    {
        Name = name;
        Size = size;
        ContentType = contentType;
        FileData = fileData;
        UploadStatus = UploadStatus.Ready;
        UploadProgress = 0;
    }

    /// <summary>
    ///     Gets the name of the file.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the size of the file in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    ///     Gets the MIME type of the file.
    /// </summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>
    ///     Gets or sets the upload status of the file.
    /// </summary>
    public UploadStatus UploadStatus { get; set; } = UploadStatus.Ready;

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
