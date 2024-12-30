namespace DropBear.Codex.Tasks.Caching;

/// <summary>
/// Configures how items are stored in the cache
/// </summary>
public class CacheEntryOptions
{
    /// <summary>
    /// Gets or sets the sliding expiration time for cache entries
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the absolute expiration time for cache entries
    /// </summary>
    public TimeSpan? AbsoluteExpiration { get; set; }

    /// <summary>
    /// Gets or sets the size of the cache entry for memory management
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    /// Gets or sets the region this cache entry belongs to
    /// </summary>
    public string? Region { get; set; }
}
