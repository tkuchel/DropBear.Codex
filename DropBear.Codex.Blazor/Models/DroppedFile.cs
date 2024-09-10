namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a file that has been dropped into a file upload control.
/// </summary>
public sealed class DroppedFile
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DroppedFile" /> class.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="size">The size of the file in bytes.</param>
    /// <param name="type">The MIME type of the file.</param>
    /// <exception cref="ArgumentException">Thrown when the name or MIME type is null or empty, or if size is negative.</exception>
    public DroppedFile(string name, long size, string type)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("File name cannot be null or empty.", nameof(name));
        }

        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "File size cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("MIME type cannot be null or empty.", nameof(type));
        }

        Name = name;
        Size = size;
        Type = type;
    }

    /// <summary>
    ///     Gets the name of the file.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    ///     Gets the size of the file in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    ///     Gets the MIME type of the file.
    /// </summary>
    public string Type { get; init; }
}
