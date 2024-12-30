namespace DropBear.Codex.Tasks.Caching;

/// <summary>
/// Internal wrapper for cache entries to track metadata for stale-while-revalidate pattern
/// </summary>
/// <typeparam name="T">The type of the cached value</typeparam>
public class CacheEntry<T>
{
    public T Value { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastRefreshed { get; set; }
    public bool IsRefreshing { get; set; }
}
