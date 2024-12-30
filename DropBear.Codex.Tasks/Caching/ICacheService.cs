#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Tasks.Caching;

/// <summary>
///     Defines the contract for a cache service that provides thread-safe caching operations
///     with support for regions and error handling
/// </summary>
public interface ICacheService : IDisposable
{
    /// <summary>
    ///     Retrieves a value from the cache by key, or creates and caches it using the provided factory if not found
    /// </summary>
    /// <typeparam name="T">The type of value being cached</typeparam>
    /// <param name="key">The unique identifier for the cached item</param>
    /// <param name="factory">A function that creates the value if it's not found in the cache</param>
    /// <param name="options">Optional configuration for how the item should be cached</param>
    /// <param name="cancellationToken">A token to cancel the operation</param>
    /// <returns>
    ///     A result containing either the cached value or an error if the operation failed
    /// </returns>
    /// <remarks>
    ///     The factory method is only called if the item isn't found in the cache.
    ///     If multiple threads request the same key simultaneously, only one will execute the factory.
    /// </remarks>
    ValueTask<Result<T, CacheError>> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes a specific item from the cache
    /// </summary>
    /// <param name="key">The key of the item to remove</param>
    /// <returns>
    ///     A result indicating whether the removal was successful
    /// </returns>
    ValueTask<Result<Unit, CacheError>> RemoveAsync(string key);

    /// <summary>
    ///     Removes all items belonging to a specific region from the cache
    /// </summary>
    /// <param name="region">The name of the region to clear</param>
    /// <returns>
    ///     A result indicating whether the region removal was successful
    /// </returns>
    /// <remarks>
    ///     This operation will remove all items that were cached with the specified region,
    ///     regardless of their individual expiration settings
    /// </remarks>
    ValueTask<Result<Unit, CacheError>> RemoveByRegionAsync(string region);

    /// <summary>
    ///     Clears all items from the cache
    /// </summary>
    /// <returns>
    ///     A result indicating whether the clear operation was successful
    /// </returns>
    /// <remarks>
    ///     This is a potentially expensive operation that will remove all cached items
    ///     regardless of their region or expiration settings
    /// </remarks>
    ValueTask<Result<Unit, CacheError>> ClearAsync();
}
