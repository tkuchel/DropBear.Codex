#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Thread-safe service for managing and caching SVG icons with optimized memory usage
///     and enhanced validation capabilities.
/// </summary>
public sealed class IconLibrary : IAsyncDisposable
{
    #region Private Methods

    /// <summary>
    ///     Throws an ObjectDisposedException if this instance has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(IconLibrary));
        }
    }

    #endregion

    #region Fields and Constants

    /// <summary>
    ///     Default time after which cached icons expire.
    /// </summary>
    private static readonly TimeSpan DefaultCacheExpiration = TimeSpan.FromHours(24);

    /// <summary>
    ///     Memory cache for storing icon SVG content.
    /// </summary>
    private readonly MemoryCache _iconCache;

    /// <summary>
    ///     Logger instance for diagnostic information.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    ///     Lock to synchronize cache operations that affect multiple items.
    /// </summary>
    private readonly SemaphoreSlim _cacheOperationLock = new(1, 1);

    /// <summary>
    ///     Tracks which icons are in the cache to avoid lookups.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _iconExistenceCache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Indicates whether this service has been disposed.
    /// </summary>
    private bool _isDisposed;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="IconLibrary" /> class with default settings.
    /// </summary>
    public IconLibrary()
        : this(LoggerFactory.Logger.ForContext<IconLibrary>())
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="IconLibrary" /> class with a specific logger.
    /// </summary>
    /// <param name="logger">The logger instance to use.</param>
    public IconLibrary(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _iconCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1000, // Limit to 1000 icons
            ExpirationScanFrequency = TimeSpan.FromHours(1),
            CompactionPercentage = 0.25 // 25% removal when limit reached
        });
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Gets an icon by its key.
    /// </summary>
    /// <param name="key">The icon key.</param>
    /// <returns>A Result containing the SVG content if successful.</returns>
    public Result<string, IconError> GetIcon(string key)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<string, IconError>.Failure(
                IconError.IconNotFound("Key cannot be empty"));
        }

        // Check existence cache first to avoid memory cache lookup (performance optimization)
        if (!_iconExistenceCache.ContainsKey(key))
        {
            return Result<string, IconError>.Failure(
                IconError.IconNotFound(key));
        }

        if (_iconCache.TryGetValue(key, out string? svgContent) && svgContent != null)
        {
            return Result<string, IconError>.Success(svgContent);
        }

        // If we get here, the icon was in the existence cache but not in the memory cache (unusual case)
        _iconExistenceCache.TryRemove(key, out _);
        return Result<string, IconError>.Failure(
            IconError.IconNotFound(key));
    }

    /// <summary>
    ///     Asynchronously gets an icon by its key.
    /// </summary>
    /// <param name="key">The icon key.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result containing the SVG content if successful.</returns>
    public Task<Result<string, IconError>> GetIconAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(Result<string, IconError>.Failure(
                IconError.IconNotFound("Key cannot be empty")));
        }

        try
        {
            // Check existence cache first to avoid memory cache lookup (performance optimization)
            if (!_iconExistenceCache.ContainsKey(key))
            {
                // Could implement asynchronous loading here from a database or remote source
                // For now, return the not found error
                return Task.FromResult(Result<string, IconError>.Failure(
                    IconError.IconNotFound(key)));
            }

            if (_iconCache.TryGetValue(key, out string? svgContent) && svgContent != null)
            {
                return Task.FromResult(Result<string, IconError>.Success(svgContent));
            }

            // If we get here, the icon was in the existence cache but not in memory cache (unusual case)
            _iconExistenceCache.TryRemove(key, out _);
            return Task.FromResult(Result<string, IconError>.Failure(
                IconError.IconNotFound(key)));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(Result<string, IconError>.Failure(
                IconError.RenderingFailed("Icon retrieval was cancelled")));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving icon: {Key}", key);
            return Task.FromResult(Result<string, IconError>.Failure(
                IconError.RenderingFailed($"Error retrieving icon: {ex.Message}"), ex));
        }
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
        ThrowIfDisposed();

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

        try
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                Size = 1,
                Priority = CacheItemPriority.Normal,
                AbsoluteExpirationRelativeToNow = cacheDuration ?? DefaultCacheExpiration,
                SlidingExpiration = TimeSpan.FromDays(1) // Reset expiration timer if accessed within a day
            };

            _iconCache.Set(key, svgContent, cacheOptions);
            _iconExistenceCache[key] = true;
            _logger.Debug("Icon registered: {Key}", key);

            return Result<bool, IconError>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error registering icon: {Key}", key);
            return Result<bool, IconError>.Failure(
                IconError.RenderingFailed($"Failed to register icon: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Asynchronously registers an icon with the library.
    /// </summary>
    /// <param name="key">The icon key.</param>
    /// <param name="svgContent">The SVG content.</param>
    /// <param name="cacheDuration">Optional cache duration.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result indicating success or failure.</returns>
    public async Task<Result<bool, IconError>> RegisterIconAsync(
        string key,
        string svgContent,
        TimeSpan? cacheDuration = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

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

        try
        {
            // Use semaphore for async operations
            await _cacheOperationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    Size = 1,
                    Priority = CacheItemPriority.Normal,
                    AbsoluteExpirationRelativeToNow = cacheDuration ?? DefaultCacheExpiration,
                    SlidingExpiration = TimeSpan.FromDays(1) // Reset expiration timer if accessed within a day
                };

                _iconCache.Set(key, svgContent, cacheOptions);
                _iconExistenceCache[key] = true;
                _logger.Debug("Icon registered asynchronously: {Key}", key);

                return Result<bool, IconError>.Success(true);
            }
            finally
            {
                _cacheOperationLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return Result<bool, IconError>.Failure(
                IconError.RenderingFailed("Icon registration was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error registering icon asynchronously: {Key}", key);
            return Result<bool, IconError>.Failure(
                IconError.RenderingFailed($"Failed to register icon: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Registers multiple icons at once.
    /// </summary>
    /// <param name="icons">Dictionary of icon keys and SVG content.</param>
    /// <returns>A Result indicating success or failure with details.</returns>
    public Result<bool, IconError> RegisterIcons(IDictionary<string, string> icons)
    {
        ThrowIfDisposed();

        if (icons == null || icons.Count == 0)
        {
            return Result<bool, IconError>.Failure(
                new IconError("No icons provided for registration"));
        }

        var errors = new List<string>();
        var successCount = 0;

        try
        {
            foreach (var (key, svg) in icons)
            {
                var result = RegisterIcon(key, svg);
                if (result.IsSuccess)
                {
                    successCount++;
                }
                else if (result.Error != null)
                {
                    errors.Add($"{key}: {result.Error.Message}");
                }
            }

            if (errors.Count > 0)
            {
                // Some registrations failed
                if (successCount > 0)
                {
                    return Result<bool, IconError>.PartialSuccess(true,
                        new IconError($"Some icons failed to register: {string.Join("; ", errors)}"));
                }

                // All registrations failed
                return Result<bool, IconError>.Failure(
                    new IconError($"All icons failed to register: {string.Join("; ", errors)}"));
            }

            return Result<bool, IconError>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error registering multiple icons");
            return Result<bool, IconError>.Failure(
                new IconError($"Failed to register icons: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Asynchronously registers multiple icons at once.
    /// </summary>
    /// <param name="icons">Dictionary of icon keys and SVG content.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result indicating success or failure.</returns>
    public async Task<Result<bool, IconError>> RegisterIconsAsync(
        IDictionary<string, string> icons,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (icons == null || icons.Count == 0)
        {
            return Result<bool, IconError>.Failure(
                new IconError("No icons provided for registration"));
        }

        var errors = new List<string>();
        var successCount = 0;

        try
        {
            // Lock to ensure consistent state
            await _cacheOperationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (var (key, svg) in icons)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    // Use the synchronous version to avoid nested locks
                    var result = RegisterIcon(key, svg);
                    if (result.IsSuccess)
                    {
                        successCount++;
                    }
                    else if (result.Error != null)
                    {
                        errors.Add($"{key}: {result.Error.Message}");
                    }
                }
            }
            finally
            {
                _cacheOperationLock.Release();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Result<bool, IconError>.Failure(
                    new IconError("Icon registration was cancelled"));
            }

            if (errors.Count > 0)
            {
                // Some registrations failed
                if (successCount > 0)
                {
                    return Result<bool, IconError>.PartialSuccess(true,
                        new IconError($"Some icons failed to register: {string.Join("; ", errors)}"));
                }

                // All registrations failed
                return Result<bool, IconError>.Failure(
                    new IconError($"All icons failed to register: {string.Join("; ", errors)}"));
            }

            return Result<bool, IconError>.Success(true);
        }
        catch (OperationCanceledException)
        {
            return Result<bool, IconError>.Failure(
                new IconError("Icon registration was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error registering multiple icons asynchronously");
            return Result<bool, IconError>.Failure(
                new IconError($"Failed to register icons: {ex.Message}"), ex);
        }
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

        // Use ReadOnlySpan to avoid string allocations in validation checks
        var content = svgContent.AsSpan();

        if (!content.Contains("<svg", StringComparison.Ordinal))
        {
            return Result<bool, IconError>.Failure(
                IconError.InvalidSvgFormat("Content does not contain SVG tag"));
        }

        // Check for potentially unsafe content (basic check)
        if (content.Contains("<script", StringComparison.Ordinal) ||
            content.Contains("javascript:", StringComparison.Ordinal))
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
    /// <returns>True if the icon was found and removed; otherwise, false.</returns>
    public bool RemoveIcon(string key)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        _iconExistenceCache.TryRemove(key, out _);
        _iconCache.Remove(key);
        _logger.Debug("Icon removed: {Key}", key);
        return true;
    }

    /// <summary>
    ///     Asynchronously removes an icon from the cache.
    /// </summary>
    /// <param name="key">The icon key to remove.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result indicating success or failure.</returns>
    public async Task<Result<bool, IconError>> RemoveIconAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<bool, IconError>.Failure(
                IconError.IconNotFound("Key cannot be empty"));
        }

        try
        {
            await _cacheOperationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var existed = _iconExistenceCache.TryRemove(key, out _);
                _iconCache.Remove(key);
                _logger.Debug("Icon removed asynchronously: {Key}", key);

                return Result<bool, IconError>.Success(existed);
            }
            finally
            {
                _cacheOperationLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return Result<bool, IconError>.Failure(
                IconError.RenderingFailed("Icon removal was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error removing icon: {Key}", key);
            return Result<bool, IconError>.Failure(
                IconError.RenderingFailed($"Failed to remove icon: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Clears all icons from the cache.
    /// </summary>
    public void ClearCache()
    {
        ThrowIfDisposed();

        _iconExistenceCache.Clear();
        _iconCache.Clear();
        _logger.Debug("Icon cache cleared");
    }

    /// <summary>
    ///     Asynchronously clears all icons from the cache.
    /// </summary>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with a Result indicating success or failure.</returns>
    public async Task<Result<Unit, IconError>> ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            await _cacheOperationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _iconExistenceCache.Clear();
                _iconCache.Clear();
                _logger.Debug("Icon cache cleared asynchronously");

                return Result<Unit, IconError>.Success(Unit.Value);
            }
            finally
            {
                _cacheOperationLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return Result<Unit, IconError>.Failure(
                IconError.RenderingFailed("Cache clearing was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error clearing icon cache");
            return Result<Unit, IconError>.Failure(
                IconError.RenderingFailed($"Failed to clear icon cache: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Gets performance metrics for the icon cache.
    /// </summary>
    /// <returns>A dictionary containing cache metrics.</returns>
    public Result<IDictionary<string, object>, IconError> GetCacheMetrics()
    {
        ThrowIfDisposed();

        try
        {
            var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["IconCount"] = _iconExistenceCache.Count, ["CurrentTime"] = DateTime.UtcNow
            };

            return Result<IDictionary<string, object>, IconError>.Success(metrics);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting cache metrics");
            return Result<IDictionary<string, object>, IconError>.Failure(
                IconError.RenderingFailed($"Failed to get cache metrics: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Asynchronously disposes this instance.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            _iconCache.Dispose();
            _iconExistenceCache.Clear();

            // Dispose the semaphore
            await _cacheOperationLock.WaitAsync().ConfigureAwait(false);
            _cacheOperationLock.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disposing IconLibrary");
        }
    }

    #endregion
}
