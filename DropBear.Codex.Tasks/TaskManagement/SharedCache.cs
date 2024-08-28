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
    public void Set<T>(string key, T value)
    {
        if (value is not null)
        {
            lock (_lock)
            {
                _cache[key] = value;
            }
        }
    }

    /// <summary>
    ///     Gets a value from the cache for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The key for the cached value.</param>
    /// <returns>The cached value of type <typeparamref name="T" />.</returns>
    /// <exception cref="KeyNotFoundException">
    ///     Thrown if the key is not found in the cache or the value is not of the expected type.
    /// </exception>
    public T Get<T>(string key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
        }

        throw new KeyNotFoundException($"Key '{key}' not found in cache or is not of type {typeof(T)}");
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
