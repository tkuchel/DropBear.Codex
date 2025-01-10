#region

using System.Collections.Concurrent;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.Caching;

/// <summary>
///     Provides thread-safe caching services with region-based cache management and background refresh capabilities
/// </summary>
public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, HashSet<string>> _regionKeys;
    private CancellationTokenSource? _cleanupCancellationTokenSource;

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

        // Initialize cleanup token source
        _cleanupCancellationTokenSource = new CancellationTokenSource();

        // Start periodic cleanup
        _ = StartPeriodicCleanupAsync(_cleanupCancellationTokenSource.Token);
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
        var metrics = new CacheMetrics($"GetOrSet<{typeof(T).Name}>");

        try
        {
            // Try to get the value from cache first
            if (_cache.TryGetValue<CacheEntry<T>>(key, out var cachedEntry))
            {
                metrics.LogCacheHit(key);

                // Check if entry is expired but within stale window
                if (cachedEntry != null && IsStale(cachedEntry, options) && !cachedEntry.IsRefreshing)
                {
                    // Trigger background refresh with error handling
                    _ = RefreshCacheInBackgroundAsync(key, factory, options, cachedEntry)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                _logger.Error(t.Exception, "Background refresh failed for key: {Key}", key);
                            }
                        }, TaskScheduler.Default);
                }

                if (cachedEntry != null)
                {
                    return Result<T, CacheError>.Success(cachedEntry.Value);
                }
            }

            metrics.LogCacheMiss(key);
            var lockObj = GetOrCreateLock(key);

            await lockObj.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Double-check pattern
                if (_cache.TryGetValue(key, out cachedEntry))
                {
                    metrics.LogCacheHit(key);
                    if (cachedEntry != null)
                    {
                        return Result<T, CacheError>.Success(cachedEntry.Value);
                    }
                }

                // Execute the factory method to get the value
                var factoryMetrics = new CacheMetrics($"Factory<{typeof(T).Name}>");
                var value = await factory(cancellationToken).ConfigureAwait(false);
                factoryMetrics.LogCacheMiss(key);

                var entry = new CacheEntry<T>
                {
                    Value = value, CreatedAt = DateTime.UtcNow, LastRefreshed = DateTime.UtcNow
                };

                // Configure and set cache entry
                var entryOptions = ConfigureCacheEntry(options);
                _cache.Set(key, entry, entryOptions);

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
    /// <param name="key">The cache key</param>
    /// <returns>A result indicating success or failure</returns>
    public ValueTask<Result<Unit, CacheError>> RemoveAsync(string key)
    {
        try
        {
            _logger.Information("Invalidating cache key: {Key}", key);
            _cache.Remove(key);
            CleanupLock(key);
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
            _logger.Information("Invalidating cache region: {Region}", region);
            if (!_regionKeys.TryGetValue(region, out var keys))
            {
                return Result<Unit, CacheError>.Success(Unit.Value);
            }

            // Create a copy of keys to avoid modification during enumeration
            var keysToRemove = keys.ToList();
            foreach (var key in keysToRemove)
            {
                _logger.Information("Invalidating key {Key} from region {Region}", key, region);
                await RemoveAsync(key).ConfigureAwait(false);
            }

            // Try to remove the region itself if it's empty
            if (_regionKeys.TryGetValue(region, out var remainingKeys) && remainingKeys.Count == 0)
            {
                _regionKeys.TryRemove(region, out _);
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
            _logger.Information("Starting cache clear operation");
            if (_cache is MemoryCache memoryCache)
            {
                _logger.Debug("Performing cache compaction");
                memoryCache.Compact(1.0);
            }

            // Clean up all tracking collections
            CleanupAllResources();

            _logger.Information("Cache clear operation completed");
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
    ///     Disposes the cache service and releases all resources
    /// </summary>
    public void Dispose()
    {
        _logger.Information("Disposing CacheService");

        // Cancel the cleanup process
        if (_cleanupCancellationTokenSource != null)
        {
            _cleanupCancellationTokenSource.Cancel();
            _cleanupCancellationTokenSource.Dispose();
            _cleanupCancellationTokenSource = null;
        }

        CleanupAllResources();
        GC.SuppressFinalize(this);
        _logger.Information("CacheService disposed");
    }

    /// <summary>
    ///     Starts the periodic cleanup process
    /// </summary>
    private async Task StartPeriodicCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(CacheDefaults.CleanupInterval, cancellationToken).ConfigureAwait(false);

                if (!cancellationToken.IsCancellationRequested)
                {
                    await PerformPeriodicCleanupAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Periodic cleanup cancelled");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during periodic cache cleanup");
        }
    }

    /// <summary>
    ///     Executes a single cleanup operation
    /// </summary>
    private Task PerformPeriodicCleanupAsync()
    {
        try
        {
            _logger.Debug("Starting periodic cache cleanup");

            // Cleanup locks for non-existent cache entries
            CleanupUnusedLocks();

            // Cleanup empty regions
            CleanupEmptyRegions();

            // Perform memory cache compaction if possible
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(0.25);
            }

            _logger.Debug("Periodic cache cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error performing periodic cleanup: {Error}", ex.Message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Cleans up all resources used by the cache
    /// </summary>
    private void CleanupAllResources()
    {
        // Clear all locks
        foreach (var lockObj in _locks.Values)
        {
            lockObj.Dispose();
        }

        _locks.Clear();

        // Clear region tracking
        _regionKeys.Clear();

        _logger.Debug("All cache resources cleaned up");
    }

    /// <summary>
    ///     Removes unused locks from the locks dictionary
    /// </summary>
    private void CleanupUnusedLocks()
    {
        foreach (var lockEntry in _locks.ToList())
        {
            if (!_cache.TryGetValue(lockEntry.Key, out _))
            {
                if (_locks.TryRemove(lockEntry.Key, out var lockObj))
                {
                    lockObj.Dispose();
                    _logger.Debug("Removed unused lock for key: {Key}", lockEntry.Key);
                }
            }
        }
    }

    /// <summary>
    ///     Removes empty regions from the region tracking dictionary
    /// </summary>
    private void CleanupEmptyRegions()
    {
        foreach (var region in _regionKeys.ToList())
        {
            if (region.Value.Count == 0)
            {
                if (_regionKeys.TryRemove(region.Key, out _))
                {
                    _logger.Debug("Removed empty region: {Region}", region.Key);
                }
            }
        }
    }

    /// <summary>
    ///     Gets or creates a lock object for the specified key
    /// </summary>
    private SemaphoreSlim GetOrCreateLock(string key)
    {
        return _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    /// <summary>
    ///     Removes and disposes of a lock for the specified key
    /// </summary>
    private void CleanupLock(string key)
    {
        if (_locks.TryRemove(key, out var lockObj))
        {
            lockObj.Dispose();
            _logger.Debug("Removed lock for key: {Key}", key);
        }
    }

    /// <summary>
    ///     Checks if a cache entry is stale based on its options
    /// </summary>
    private static bool IsStale<T>(CacheEntry<T> entry, CacheEntryOptions? options)
    {
        if (options?.StaleWhileRevalidate == null || entry.LastRefreshed == null)
        {
            return false;
        }

        var staleness = DateTime.UtcNow - entry.LastRefreshed.Value;
        var totalAllowedStaleness = options.SlidingExpiration + options.StaleWhileRevalidate;

        return staleness > options.SlidingExpiration && staleness <= totalAllowedStaleness;
    }

    /// <summary>
    ///     Refreshes a cache entry in the background
    /// </summary>
    private async Task RefreshCacheInBackgroundAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CacheEntryOptions? options,
        CacheEntry<T> existingEntry)
    {
        try
        {
            // Mark as refreshing to prevent multiple refresh attempts
            existingEntry.IsRefreshing = true;

            // Create a new cancellation token with configured timeout
            using var cts = new CancellationTokenSource(CacheDefaults.RefreshTimeout);

            _logger.Information("Starting background refresh for cache key: {Key}", key);

            // Execute factory to get fresh value
            var freshValue = await factory(cts.Token).ConfigureAwait(false);

            var lockObj = GetOrCreateLock(key);
            await lockObj.WaitAsync(cts.Token).ConfigureAwait(false);
            try
            {
                // Update the existing entry
                existingEntry.Value = freshValue;
                existingEntry.LastRefreshed = DateTime.UtcNow;
                existingEntry.IsRefreshing = false;

                // Reconfigure cache options
                var entryOptions = ConfigureCacheEntry(options);

                // Store updated entry
                _cache.Set(key, existingEntry, entryOptions);

                _logger.Information("Successfully refreshed cache key: {Key}", key);
            }
            finally
            {
                lockObj.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Background refresh timed out for key: {Key}", key);
            existingEntry.IsRefreshing = false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error refreshing cache in background for key: {Key}", key);
            existingEntry.IsRefreshing = false;

            // Consider removing the entry if refresh failed
            try
            {
                await RemoveAsync(key).ConfigureAwait(false);
                _logger.Information("Removed failed cache entry for key: {Key}", key);
            }
            catch (Exception removeEx)
            {
                _logger.Error(removeEx, "Failed to remove failed cache entry for key: {Key}", key);
            }
        }
    }

    /// <summary>
    ///     Associates a cache key with a region for grouped operations
    /// </summary>
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

        _logger.Debug("Added key {Key} to region {Region}", key, region);
    }

    /// <summary>
    ///     Configures cache entry options based on provided settings with sensible defaults
    /// </summary>
    private static MemoryCacheEntryOptions ConfigureCacheEntry(CacheEntryOptions? options)
    {
        var entryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = options?.SlidingExpiration ?? CacheDefaults.SlidingExpiration,
            AbsoluteExpirationRelativeToNow = options?.AbsoluteExpiration ?? CacheDefaults.AbsoluteExpiration,
            Size = options?.Size ?? CacheDefaults.DefaultSize
        };

        return entryOptions;
    }

    /// <summary>
    ///     Default cache configuration settings
    /// </summary>
    private static class CacheDefaults
    {
        /// <summary>
        ///     Default size for cache entries when not specified
        /// </summary>
        public const long DefaultSize = 1;

        /// <summary>
        ///     Default sliding expiration time for cache entries
        /// </summary>
        public static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);

        /// <summary>
        ///     Default absolute expiration time for cache entries
        /// </summary>
        public static readonly TimeSpan AbsoluteExpiration = TimeSpan.FromHours(24);

        /// <summary>
        ///     Default timeout for background refresh operations
        /// </summary>
        public static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        ///     Interval for automated cleanup operations
        /// </summary>
        public static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);
    }
}
