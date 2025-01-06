namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a file that has been dropped into a file upload control,
///     including its name, size, MIME type, and raw data.
/// </summary>
public sealed class DroppedFile
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DroppedFile" /> class.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="size">The size of the file in bytes.</param>
    /// <param name="type">The MIME type of the file (e.g., "image/png").</param>
    /// <param name="data">The raw byte content of the file (can be null if not loaded).</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="name" /> or <paramref name="type" /> is null or empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="size" /> is negative.</exception>
    public DroppedFile(string name, long size, string type, byte[]? data)
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
        Data = data; // Byte array can be null if no data is loaded yet
    }

    /// <summary>
    ///     Gets the file name (without path).
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    ///     Gets the size of the file in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    ///     Gets the MIME type of the file (e.g., image/png).
    /// </summary>
    public string Type { get; init; }

    /// <summary>
    ///     Gets the raw byte content of the file, if loaded.
    ///     May be null if the file has not been fully read into memory.
    /// </summary>
    public byte[]? Data { get; init; }
}
