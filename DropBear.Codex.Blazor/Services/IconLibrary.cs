#region

using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Service for managing SVG icons with caching and validation.
/// </summary>
public sealed class IconLibrary
{
    private static readonly TimeSpan DefaultCacheExpiration = TimeSpan.FromHours(24);
    private readonly MemoryCache _iconCache;
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="IconLibrary" /> class.
    /// </summary>
    public IconLibrary()
    {
        _logger = LoggerFactory.Logger.ForContext<IconLibrary>();
        _iconCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1000, // Limit to 1000 icons
            ExpirationScanFrequency = TimeSpan.FromHours(1)
        });
    }

    /// <summary>
    ///     Gets an icon by its key.
    /// </summary>
    /// <param name="key">The icon key.</param>
    /// <returns>A Result containing the SVG content if successful.</returns>
    public Result<string, IconError> GetIcon(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<string, IconError>.Failure(
                IconError.IconNotFound("Key cannot be empty"));
        }

        if (_iconCache.TryGetValue(key, out string? svgContent) && svgContent != null)
        {
            return Result<string, IconError>.Success(svgContent);
        }

        return Result<string, IconError>.Failure(
            IconError.IconNotFound(key));
    }

    /// <summary>
    ///     Registers an icon with the library.
    /// </summary>
    /// <param name="key">The icon key.</param>
    /// <param name="svgContent">The SVG content.</param>
    /// <param name="cacheDuration">Optional cache duration.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result<bool, IconError> RegisterIcon(
        string key,
        string svgContent,
        TimeSpan? cacheDuration = null)
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

        var cacheOptions = new MemoryCacheEntryOptions
        {
            Size = 1, AbsoluteExpirationRelativeToNow = cacheDuration ?? DefaultCacheExpiration
        };

        _iconCache.Set(key, svgContent, cacheOptions);
        _logger.Debug("Icon registered: {Key}", key);

        return Result<bool, IconError>.Success(true);
    }

    /// <summary>
    ///     Registers multiple icons at once.
    /// </summary>
    /// <param name="icons">Dictionary of icon keys and SVG content.</param>
    /// <returns>A Result indicating success or failure with details.</returns>
    public Result<bool, IconError> RegisterIcons(Dictionary<string, string> icons)
    {
        if (icons == null || icons.Count == 0)
        {
            return Result<bool, IconError>.Failure(
                new IconError("No icons provided for registration"));
        }

        var errors = new List<string>();
        foreach (var (key, svg) in icons)
        {
            var result = RegisterIcon(key, svg);
            if (!result.IsSuccess && result.Error != null)
            {
                errors.Add($"{key}: {result.Error.Message}");
            }
        }

        if (errors.Count > 0)
        {
            return Result<bool, IconError>.PartialSuccess(true,
                new IconError($"Some icons failed to register: {string.Join("; ", errors)}"));
        }

        return Result<bool, IconError>.Success(true);
    }

    /// <summary>
    ///     Validates SVG content for security and correctness.
    /// </summary>
    /// <param name="svgContent">The SVG content to validate.</param>
    /// <returns>A Result indicating success or validation errors.</returns>
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

        // Check for potentially unsafe content (basic check)
        if (svgContent.Contains("<script") || svgContent.Contains("javascript:"))
        {
            return Result<bool, IconError>.Failure(
                IconError.InvalidSvgFormat("SVG contains potentially unsafe script content"));
        }

        return Result<bool, IconError>.Success(true);
    }

    /// <summary>
    ///     Removes an icon from the cache.
    /// </summary>
    /// <param name="key">The icon key to remove.</param>
    public void RemoveIcon(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _iconCache.Remove(key);
            _logger.Debug("Icon removed: {Key}", key);
        }
    }

    /// <summary>
    ///     Clears all icons from the cache.
    /// </summary>
    public void ClearCache()
    {
        _iconCache.Clear();
        _logger.Debug("Icon cache cleared");
    }
}
