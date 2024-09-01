namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a file that has been dropped into a file upload control.
/// </summary>
public sealed class DroppedFile
{
    public DroppedFile()
    {
        // Intentionally left blank
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DroppedFile" /> class.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="size">The size of the file in bytes.</param>
    /// <param name="type">The MIME type of the file.</param>
    public DroppedFile(string name, long size, string type)
    {
        Name = name;
        Size = size;
        Type = type;
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
    public string Type { get; init; } = string.Empty;
}
