#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents errors that occur during icon rendering operations.
/// </summary>
public sealed record IconError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="IconError" /> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public IconError(string message) : base(message) { }

    /// <summary>
    ///     Creates an error for when an icon cannot be found.
    /// </summary>
    /// <param name="iconName">The name of the icon that could not be found.</param>
    /// <returns>A new <see cref="IconError" /> with appropriate message.</returns>
    public static IconError IconNotFound(string iconName)
    {
        return new IconError($"Icon '{iconName}' could not be found");
    }

    /// <summary>
    ///     Creates an error for when an SVG has invalid format.
    /// </summary>
    /// <param name="details">Details about the validation failure.</param>
    /// <returns>A new <see cref="IconError" /> with appropriate message.</returns>
    public static IconError InvalidSvgFormat(string details)
    {
        return new IconError($"Invalid SVG format: {details}");
    }

    /// <summary>
    ///     Creates an error for general rendering failures.
    /// </summary>
    /// <param name="details">Details about the rendering failure.</param>
    /// <returns>A new <see cref="IconError" /> with appropriate message.</returns>
    public static IconError RenderingFailed(string details)
    {
        return new IconError($"Icon rendering failed: {details}");
    }
}
