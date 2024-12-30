namespace DropBear.Codex.Tasks.Caching;

/// <summary>
///     Configures how items are stored in the cache
/// </summary>
public class CacheEntryOptions
{
    /// <summary>
    ///     Gets or sets the sliding expiration time for cache entries
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    ///     Gets or sets the absolute expiration time for cache entries
    /// </summary>
    public TimeSpan? AbsoluteExpiration { get; set; }

    /// <summary>
    ///     Gets or sets the size of the cache entry for memory management
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    ///     Gets or sets the region this cache entry belongs to
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    ///     Time span during which a stale (expired) cache entry can still be served while a background refresh is attempted.
    ///     This helps prevent cache stampedes and provides a better user experience by serving slightly stale data
    ///     rather than making users wait for fresh data.
    /// </summary>
    /// <remarks>
    ///     When set:
    ///     - If the cache entry is expired but within the stale window, the stale value will be returned
    ///     - A background task will be triggered to refresh the cache
    ///     - Subsequent requests will get the stale data until the refresh completes
    ///     - Once refreshed, new requests will get the fresh data
    /// </remarks>
    /// <example>
    ///     Setting a 5-minute stale window:
    ///     <code>
    /// new CacheEntryOptions
    /// {
    ///     SlidingExpiration = TimeSpan.FromMinutes(30),
    ///     StaleWhileRevalidate = TimeSpan.FromMinutes(5)
    /// }
    /// </code>
    /// </example>
    public TimeSpan? StaleWhileRevalidate { get; set; }
}
