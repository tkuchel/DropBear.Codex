#region

using System.Collections.Concurrent;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.Caching;

/// <summary>
///     Provides caching services with thread-safe operations and region-based cache management
/// </summary>
public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, HashSet<string>> _regionKeys;

    /// <summary>
    ///     Initializes a new instance of the CacheService
    /// </summary>
    /// <param name="cache">The memory cache implementation to use</param>
    /// <exception cref="ArgumentNullException">Thrown when cache is null</exception>
    public CacheService(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = LoggerFactory.Logger.ForContext<CacheService>();
        _locks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
        _regionKeys = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.Ordinal);
    }

    /// <summary>
    ///     Gets a value from the cache or sets it using the provided factory if it doesn't exist
    /// </summary>
    /// <typeparam name="T">The type of value being cached</typeparam>
    /// <param name="key">The cache key</param>
    /// <param name="factory">A factory function to create the value if it's not in the cache</param>
    /// <param name="options">Optional cache entry configuration</param>
    /// <param name="cancellationToken">A token to cancel the operation</param>
    /// <returns>A result containing either the cached value or an error</returns>
    public async ValueTask<Result<T, CacheError>> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get the value from cache first
            if (_cache.TryGetValue<T>(key, out var cachedValue) && cachedValue is not null)
            {
                return Result<T, CacheError>.Success(cachedValue);
            }

            // Get or create a lock for this key
            var lockObj = _locks.GetOrAdd(key, k => new SemaphoreSlim(1, 1));

            // Ensure only one thread can update the cache at a time
            await lockObj.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Double-check pattern
                if (_cache.TryGetValue(key, out cachedValue) && cachedValue is not null)
                {
                    return Result<T, CacheError>.Success(cachedValue);
                }

                // Execute the factory method to get the value
                var value = await factory(cancellationToken).ConfigureAwait(false);

                // Configure cache entry options
                var entryOptions = ConfigureCacheEntry(options);

                // Store in cache
                _cache.Set(key, value, entryOptions);

                // Track region if specified
                if (!string.IsNullOrEmpty(options?.Region))
                {
                    TrackKeyInRegion(options.Region, key);
                }

                return Result<T, CacheError>.Success(value);
            }
            finally
            {
                lockObj.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error while accessing cache for key: {Key}", key);
            return Result<T, CacheError>.Failure(CacheError.OperationFailed(ex.Message), ex);
        }
    }

    /// <summary>
    ///     Removes an item from the cache by key
    /// </summary>
    /// <param name="key">The key to remove</param>
    /// <returns>A result indicating success or failure</returns>
    public ValueTask<Result<Unit, CacheError>> RemoveAsync(string key)
    {
        try
        {
            _cache.Remove(key);
            return ValueTask.FromResult(Result<Unit, CacheError>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error removing cache entry: {Key}", key);
            return ValueTask.FromResult(Result<Unit, CacheError>.Failure(
                CacheError.OperationFailed($"Failed to remove key: {key}"),
                ex));
        }
    }

    /// <summary>
    ///     Removes all items from the cache that belong to the specified region
    /// </summary>
    /// <param name="region">The region to clear</param>
    /// <returns>A result indicating success or failure</returns>
    public async ValueTask<Result<Unit, CacheError>> RemoveByRegionAsync(string region)
    {
        try
        {
            if (!_regionKeys.TryGetValue(region, out var keys))
            {
                return Result<Unit, CacheError>.Success(Unit.Value);
            }

            foreach (var key in keys)
            {
                await RemoveAsync(key).ConfigureAwait(false);
            }

            return Result<Unit, CacheError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error removing cache entries for region: {Region}", region);
            return Result<Unit, CacheError>.Failure(
                CacheError.OperationFailed($"Failed to remove region: {region}"),
                ex);
        }
    }

    /// <summary>
    ///     Clears all items from the cache
    /// </summary>
    /// <returns>A result indicating success or failure</returns>
    public ValueTask<Result<Unit, CacheError>> ClearAsync()
    {
        try
        {
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0);
            }

            return ValueTask.FromResult(Result<Unit, CacheError>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error clearing cache");
            return ValueTask.FromResult(Result<Unit, CacheError>.Failure(
                CacheError.OperationFailed("Failed to clear cache"),
                ex));
        }
    }

    /// <summary>
    ///     Disposes the cache service and releases all locks
    /// </summary>
    public void Dispose()
    {
        foreach (var lockObj in _locks.Values)
        {
            lockObj.Dispose();
        }

        _locks.Clear();
        _regionKeys.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Associates a cache key with a region for grouped operations
    /// </summary>
    /// <param name="region">The region name</param>
    /// <param name="key">The cache key to track</param>
    private void TrackKeyInRegion(string region, string key)
    {
        _regionKeys.AddOrUpdate(
            region,
            _ => new HashSet<string>(StringComparer.Ordinal) { key },
            (_, keys) =>
            {
                lock (keys)
                {
                    keys.Add(key);
                    return keys;
                }
            });
    }

    /// <summary>
    ///     Configures cache entry options based on provided settings
    /// </summary>
    /// <param name="options">The cache options to apply</param>
    /// <returns>Configured MemoryCacheEntryOptions</returns>
    private MemoryCacheEntryOptions ConfigureCacheEntry(CacheEntryOptions? options)
    {
        var entryOptions = new MemoryCacheEntryOptions();

        if (options?.SlidingExpiration != null)
        {
            entryOptions.SlidingExpiration = options.SlidingExpiration;
        }

        if (options?.AbsoluteExpiration != null)
        {
            entryOptions.AbsoluteExpirationRelativeToNow = options.AbsoluteExpiration;
        }

        if (options?.Size != null)
        {
            entryOptions.Size = options.Size.Value;
        }

        entryOptions.RegisterPostEvictionCallback(OnPostEviction);

        return entryOptions;
    }

    /// <summary>
    ///     Handles cleanup when cache entries are evicted
    /// </summary>
    private void OnPostEviction(object key, object? value, EvictionReason reason, object? state)
    {
        if (key is string cacheKey)
        {
            foreach (var region in _regionKeys)
            {
                region.Value.Remove(cacheKey);
            }
        }
    }
}
