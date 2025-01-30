using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components.Forms;

namespace DropBear.Codex.Blazor.Models;

/// <summary>
/// Represents a file selected for upload with its associated metadata and status.
/// </summary>
public sealed class UploadFile
{
    /// <summary>
    /// Creates a new instance of the UploadFile class.
    /// </summary>
    public UploadFile(string name, long size, string? contentType, IBrowserFile file)
    {
        Name = name;
        Size = size;
        ContentType = contentType;
        File = file;
        UploadStatus = UploadStatus.Pending;
        UploadProgress = 0;
    }

    /// <summary>
    /// Gets the name of the file.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the size of the file in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Gets the content type (MIME type) of the file.
    /// </summary>
    public string? ContentType { get; }

    /// <summary>
    /// Gets the underlying browser file object.
    /// </summary>
    public IBrowserFile File { get; }

    /// <summary>
    /// Gets or sets the current upload status.
    /// </summary>
    public UploadStatus UploadStatus { get; set; }

    /// <summary>
    /// Gets or sets the upload progress as a percentage (0-100).
    /// </summary>
    public int UploadProgress { get; set; }
}
