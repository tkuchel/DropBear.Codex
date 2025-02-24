namespace DropBear.Codex.Tasks.Caching;

/// <summary>
///     Internal wrapper for cache entries to track metadata for a stale-while-revalidate pattern.
///     <para>
///         Stores a cached <typeparamref name="T" /> along with timestamps indicating creation and last refresh time,
///         plus a flag to detect when a refresh is in progress (e.g., background updates).
///     </para>
/// </summary>
/// <typeparam name="T">The type of the cached value.</typeparam>
public sealed class CacheEntry<T>
{
    /// <summary>
    ///     The actual cached value.
    /// </summary>
    public T Value { get; set; } = default!;

    /// <summary>
    ///     The time (UTC) at which the entry was first created in the cache.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     The time (UTC) at which the cache entry was last refreshed, or <c>null</c> if never refreshed.
    /// </summary>
    public DateTime? LastRefreshed { get; set; }

    /// <summary>
    ///     Indicates whether a background refresh is currently in progress for this entry.
    /// </summary>
    public bool IsRefreshing { get; set; }
}
