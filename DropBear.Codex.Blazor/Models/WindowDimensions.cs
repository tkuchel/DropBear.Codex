namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the dimensions (width and height) of a window (e.g. a browser window).
/// </summary>
public sealed class WindowDimensions
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="WindowDimensions" /> class
    ///     with the specified <paramref name="width" /> and <paramref name="height" />.
    /// </summary>
    /// <param name="width">The width of the window in pixels.</param>
    /// <param name="height">The height of the window in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown if <paramref name="width" /> or <paramref name="height" /> is negative.
    /// </exception>
    public WindowDimensions(int width, int height)
    {
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width cannot be negative.");
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height cannot be negative.");
        }

        Width = width;
        Height = height;
    }

    /// <summary>
    ///     Gets the width of the window in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    ///     Gets the height of the window in pixels.
    /// </summary>
    public int Height { get; }
}
