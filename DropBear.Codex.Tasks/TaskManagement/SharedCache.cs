#region

using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Caching;

#endregion

namespace DropBear.Codex.Tasks.TaskManagement;

/// <summary>
///     Provides a simple in-memory cache for storing and retrieving objects by key.
/// </summary>
public sealed class SharedCache
{
    private readonly Dictionary<string, object> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>
    ///     Sets a value in the cache for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The key for the cached value.</param>
    /// <param name="value">The value to be cached.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result<Unit, CacheError> Set<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<Unit, CacheError>.Failure(
                CacheError.OperationFailed("Cache key cannot be null or empty."));
        }

        if (value is null)
        {
            return Result<Unit, CacheError>.Failure(
                CacheError.OperationFailed("Cache value cannot be null."));
        }

        try
        {
            lock (_lock)
            {
                _cache[key] = value;
            }
            return Result<Unit, CacheError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, CacheError>.Failure(
                CacheError.OperationFailed($"Failed to set cache value: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Gets a value from the cache for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The key for the cached value.</param>
    /// <returns>
    ///     A result containing the cached value of type <typeparamref name="T" /> if successful,
    ///     or an error if the key is not found or the value is not of the expected type.
    /// </returns>
    public Result<T, CacheError> Get<T>(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<T, CacheError>.Failure(
                CacheError.OperationFailed("Cache key cannot be null or empty."));
        }

        try
        {
            lock (_lock)
            {
                if (!_cache.TryGetValue(key, out var value))
                {
                    return Result<T, CacheError>.Failure(CacheError.NotFound(key));
                }

                if (value is not T typedValue)
                {
                    return Result<T, CacheError>.Failure(
                        CacheError.TypeMismatch(key, typeof(T).Name));
                }

                return Result<T, CacheError>.Success(typedValue);
            }
        }
        catch (Exception ex)
        {
            return Result<T, CacheError>.Failure(
                CacheError.OperationFailed($"Failed to get cache value: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Attempts to get a value from the cache for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The key for the cached value.</param>
    /// <param name="value">The retrieved value, if found.</param>
    /// <returns>True if the value was found and is of the expected type; otherwise, false.</returns>
    public bool TryGet<T>(string key, out T? value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var objValue) && objValue is T typedValue)
            {
                value = typedValue;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    ///     Removes a value from the cache for the specified key.
    /// </summary>
    /// <param name="key">The key for the cached value.</param>
    /// <returns>True if the value was removed; otherwise, false.</returns>
    public bool Remove(string key)
    {
        lock (_lock)
        {
            return _cache.Remove(key);
        }
    }
}
