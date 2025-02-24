namespace DropBear.Codex.Tasks.Caching;

/// <summary>
///     Configures how items are stored in the cache, including expiration policies and stale-while-revalidate behavior.
/// </summary>
public class CacheEntryOptions
{
    /// <summary>
    ///     Gets or sets the sliding expiration time for cache entries, after which the entry
    ///     will expire if it hasn't been accessed within this duration.
    /// </summary>
    /// <remarks>
    ///     If left <c>null</c>, a default sliding expiration may be used (see <c>CacheService</c>).
    /// </remarks>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    ///     Gets or sets the absolute expiration time for cache entries, after which the entry
    ///     will expire regardless of whether it's been accessed.
    /// </summary>
    /// <remarks>If left <c>null</c>, a default absolute expiration may be used.</remarks>
    public TimeSpan? AbsoluteExpiration { get; set; }

    /// <summary>
    ///     Gets or sets the size of the cache entry for memory management.
    ///     Typically used by implementations that track memory usage.
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    ///     Gets or sets the region this cache entry belongs to, allowing grouped operations
    ///     for invalidation or retrieval.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    ///     Time span during which a stale (expired) cache entry can still be served while a background refresh is attempted.
    ///     Helps prevent cache stampedes, providing a better user experience by serving slightly stale data
    ///     rather than blocking on a fresh fetch.
    /// </summary>
    /// <remarks>
    ///     If set to a non-null <see cref="TimeSpan" />:
    ///     <list type="bullet">
    ///         <item>Expired entries are served if within the stale window.</item>
    ///         <item>A background task is triggered to refresh the cache.</item>
    ///         <item>Subsequent requests get the stale data until refresh completes.</item>
    ///     </list>
    /// </remarks>
    public TimeSpan? StaleWhileRevalidate { get; set; }
}
