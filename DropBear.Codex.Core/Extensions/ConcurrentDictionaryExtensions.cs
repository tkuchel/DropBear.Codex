#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

#endregion

namespace DropBear.Codex.Core.Extensions;

/// <summary>
///     Extension methods for ConcurrentDictionary optimized for .NET 9.
///     Provides enhanced async operations with better performance.
/// </summary>
public static class ConcurrentDictionaryExtensions
{
    /// <summary>
    ///     Asynchronously gets or adds a value to the dictionary using ValueTask.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="valueFactory">Async factory function for creating the value.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The existing or newly created value.</returns>
    public static async ValueTask<TValue> GetOrAddAsync<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TKey, CancellationToken, ValueTask<TValue>> valueFactory,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(valueFactory);

        // Fast path: try to get existing value
        if (dictionary.TryGetValue(key, out var existingValue))
        {
            return existingValue;
        }

        // Slow path: create new value
        var newValue = await valueFactory(key, cancellationToken).ConfigureAwait(false);
        return dictionary.GetOrAdd(key, newValue);
    }

    /// <summary>
    ///     Asynchronously gets or adds a value using a simpler factory signature.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<TValue> GetOrAddAsync<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TKey, ValueTask<TValue>> valueFactory)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(valueFactory);

        if (dictionary.TryGetValue(key, out var existingValue))
        {
            return existingValue;
        }

        var newValue = await valueFactory(key).ConfigureAwait(false);
        return dictionary.GetOrAdd(key, newValue);
    }

    /// <summary>
    ///     Asynchronously gets or adds a value using Task-based factory (legacy support).
    /// </summary>
    public static async Task<TValue> GetOrAddAsync<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TKey, Task<TValue>> valueFactory)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(valueFactory);

        if (dictionary.TryGetValue(key, out var existingValue))
        {
            return existingValue;
        }

        var newValue = await valueFactory(key).ConfigureAwait(false);
        return dictionary.GetOrAdd(key, newValue);
    }

    /// <summary>
    ///     Asynchronously updates an existing value or adds a new one.
    /// </summary>
    public static async ValueTask<TValue> AddOrUpdateAsync<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TKey, ValueTask<TValue>> addFactory,
        Func<TKey, TValue, ValueTask<TValue>> updateFactory,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(addFactory);
        ArgumentNullException.ThrowIfNull(updateFactory);

        while (true)
        {
            if (dictionary.TryGetValue(key, out var existingValue))
            {
                var newValue = await updateFactory(key, existingValue).ConfigureAwait(false);

                if (dictionary.TryUpdate(key, newValue, existingValue))
                {
                    return newValue;
                }

                // Value was modified by another thread, retry
                continue;
            }

            var addedValue = await addFactory(key).ConfigureAwait(false);

            if (dictionary.TryAdd(key, addedValue))
            {
                return addedValue;
            }

            // Key was added by another thread, retry with update
        }
    }

    /// <summary>
    ///     Asynchronously removes and returns a value if the predicate matches.
    /// </summary>
    public static async ValueTask<(bool Removed, TValue? Value)> TryRemoveAsync<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TValue, ValueTask<bool>> predicate,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(predicate);

        if (!dictionary.TryGetValue(key, out var value))
        {
            return (false, default);
        }

        var shouldRemove = await predicate(value).ConfigureAwait(false);

        if (shouldRemove && dictionary.TryRemove(key, out var removedValue))
        {
            return (true, removedValue);
        }

        return (false, default);
    }

    /// <summary>
    ///     Processes all items in the dictionary asynchronously.
    /// </summary>
    public static async ValueTask ForEachAsync<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        Func<TKey, TValue, ValueTask> action,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(action);

        foreach (var (key, value) in dictionary)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action(key, value).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Processes all items in the dictionary in parallel.
    /// </summary>
    public static async ValueTask ParallelForEachAsync<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        Func<TKey, TValue, CancellationToken, ValueTask> action,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(action);

        await Parallel.ForEachAsync(
            dictionary,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism, CancellationToken = cancellationToken
            },
            async (kvp, ct) => await action(kvp.Key, kvp.Value, ct).ConfigureAwait(false)
        ).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets all values that match the async predicate.
    /// </summary>
    public static async ValueTask<List<TValue>> WhereAsync<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        Func<TKey, TValue, ValueTask<bool>> predicate,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(predicate);

        var results = new List<TValue>();

        foreach (var (key, value) in dictionary)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await predicate(key, value).ConfigureAwait(false))
            {
                results.Add(value);
            }
        }

        return results;
    }

    /// <summary>
    ///     Transforms all values in the dictionary asynchronously.
    /// </summary>
    public static async ValueTask<Dictionary<TKey, TResult>> SelectAsync<TKey, TValue, TResult>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        Func<TKey, TValue, ValueTask<TResult>> selector,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(selector);

        var results = new Dictionary<TKey, TResult>(dictionary.Count);

        foreach (var (key, value) in dictionary)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await selector(key, value).ConfigureAwait(false);
            results[key] = result;
        }

        return results;
    }
}
