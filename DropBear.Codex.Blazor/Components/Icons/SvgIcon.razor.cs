#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Components.Icons;

/// <summary>
///     Component for rendering SVG icons with enhanced accessibility and customization.
/// </summary>
public partial class SvgIcon : DropBearComponentBase
{
    /// <summary>
    ///     Validates that the provided SVG content follows best practices.
    /// </summary>
    /// <param name="svgContent">The SVG content to validate.</param>
    /// <returns>A Result indicating success or detailed error information.</returns>
    public static Result<bool, IconError> ValidateSvgContent(string svgContent)
    {
        if (string.IsNullOrWhiteSpace(svgContent))
        {
            return Result<bool, IconError>.Failure(
                IconError.InvalidSvgFormat("SVG content is empty"));
        }

        if (!svgContent.Contains("<svg"))
        {
            return Result<bool, IconError>.Failure(
                IconError.InvalidSvgFormat("Content does not contain SVG tag"));
        }

        return Result<bool, IconError>.Success(true);
    }

    /// <summary>
    ///     Registers a custom SVG icon for use with the component.
    /// </summary>
    /// <param name="key">The unique identifier for the icon.</param>
    /// <param name="svgContent">The SVG content to register.</param>
    /// <returns>A Result indicating success or detailed error information.</returns>
    public static Result<bool, IconError> RegisterCustomIcon(string key, string svgContent)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<bool, IconError>.Failure(
                IconError.InvalidSvgFormat("Icon key cannot be empty"));
        }

        var validationResult = ValidateSvgContent(svgContent);
        if (!validationResult.IsSuccess)
        {
            return validationResult;
        }

        SvgCache[key] = svgContent;
        return Result<bool, IconError>.Success(true);
    }

    /// <summary>
    ///     Clears the icon cache to force re-fetching of icons.
    /// </summary>
    public static void ClearCache()
    {
        SvgCache.Clear();
    }
}
