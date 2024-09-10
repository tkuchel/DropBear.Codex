namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the dimensions of a window (e.g., browser window).
/// </summary>
public sealed class WindowDimensions
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="WindowDimensions" /> class.
    /// </summary>
    /// <param name="width">The width of the window.</param>
    /// <param name="height">The height of the window.</param>
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
    ///     Gets the width of the window.
    /// </summary>
    public int Width { get; }

    /// <summary>
    ///     Gets the height of the window.
    /// </summary>
    public int Height { get; }
}
