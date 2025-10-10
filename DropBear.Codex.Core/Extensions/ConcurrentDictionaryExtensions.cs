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
    /// <exception cref="ArgumentNullException">Thrown when dictionary or valueFactory is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
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

        // Check cancellation before expensive operation
        cancellationToken.ThrowIfCancellationRequested();

        // Slow path: create new value
        var newValue = await valueFactory(key, cancellationToken).ConfigureAwait(false);
        return dictionary.GetOrAdd(key, newValue);
    }

    /// <summary>
    ///     Asynchronously gets or adds a value using a simpler factory signature.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="valueFactory">Async factory function for creating the value.</param>
    /// <returns>The existing or newly created value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when dictionary or valueFactory is null.</exception>
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
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="valueFactory">Async factory function for creating the value.</param>
    /// <returns>The existing or newly created value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when dictionary or valueFactory is null.</exception>
    /// <remarks>
    ///     This overload is provided for compatibility with Task-based APIs.
    ///     Prefer using the ValueTask-based overloads for better performance.
    /// </remarks>
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
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key to add or update.</param>
    /// <param name="addFactory">Factory function to create a new value if the key doesn't exist.</param>
    /// <param name="updateFactory">Factory function to update an existing value.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The added or updated value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when dictionary, addFactory, or updateFactory is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <remarks>
    ///     This method uses optimistic concurrency control with retry logic.
    ///     If another thread modifies the value between read and write, the operation retries.
    ///     The operation will be cancelled if the <paramref name="cancellationToken" /> is triggered.
    ///     In high-contention scenarios, consider implementing a maximum retry count or exponential backoff.
    /// </remarks>
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
            // Check cancellation at the start of each retry iteration
            cancellationToken.ThrowIfCancellationRequested();

            if (dictionary.TryGetValue(key, out var existingValue))
            {
                var newValue = await updateFactory(key, existingValue).ConfigureAwait(false);

                // Check cancellation after async operation
                cancellationToken.ThrowIfCancellationRequested();

                if (dictionary.TryUpdate(key, newValue, existingValue))
                {
                    return newValue;
                }

                // Value was modified by another thread, retry
                continue;
            }

            var addedValue = await addFactory(key).ConfigureAwait(false);

            // Check cancellation after async operation
            cancellationToken.ThrowIfCancellationRequested();

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
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key to potentially remove.</param>
    /// <param name="predicate">Async predicate to determine if the value should be removed.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    ///     A tuple containing a boolean indicating if the value was removed and the removed value (if any).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when dictionary or predicate is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <remarks>
    ///     This method is not atomic. Another thread may modify the value between the predicate check
    ///     and the removal attempt. If atomicity is required, consider using TryRemove with a comparison value.
    /// </remarks>
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

        // Check cancellation before expensive async operation
        cancellationToken.ThrowIfCancellationRequested();

        var shouldRemove = await predicate(value).ConfigureAwait(false);

        // Check cancellation after async operation
        cancellationToken.ThrowIfCancellationRequested();

        if (shouldRemove && dictionary.TryRemove(key, out var removedValue))
        {
            return (true, removedValue);
        }

        return (false, default);
    }

    /// <summary>
    ///     Processes all items in the dictionary asynchronously in sequential order.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="action">Async action to perform on each key-value pair.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when dictionary or action is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <remarks>
    ///     Items are processed sequentially. The enumeration is a snapshot and will not reflect
    ///     concurrent modifications to the dictionary. Any exceptions thrown by the action will propagate.
    /// </remarks>
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
    ///     Processes all items in the dictionary in parallel with controlled concurrency.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="action">Async action to perform on each key-value pair.</param>
    /// <param name="maxDegreeOfParallelism">
    ///     Maximum number of concurrent operations.
    ///     Use -1 for unlimited parallelism (default).
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when dictionary or action is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <remarks>
    ///     Items are processed in parallel. The enumeration is a snapshot and will not reflect
    ///     concurrent modifications to the dictionary. Any exceptions thrown by the action will propagate
    ///     and cancel remaining operations.
    /// </remarks>
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
    ///     Filters the dictionary to return all values that match the async predicate.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="predicate">Async predicate to filter values.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A list containing all values that match the predicate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when dictionary or predicate is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <remarks>
    ///     The returned list is a snapshot taken during enumeration.
    ///     Concurrent modifications to the dictionary will not be reflected.
    ///     Items are evaluated sequentially in enumeration order.
    /// </remarks>
#pragma warning disable MA0016
    public static async ValueTask<List<TValue>> WhereAsync<TKey, TValue>(
#pragma warning restore MA0016
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
    ///     Transforms all key-value pairs in the dictionary asynchronously.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The source value type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="selector">Async function to transform each value.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A dictionary containing the transformed key-value pairs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when dictionary or selector is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <remarks>
    ///     The returned dictionary is a snapshot taken during enumeration.
    ///     Concurrent modifications to the source dictionary will not be reflected.
    ///     Keys are preserved, only values are transformed.
    ///     Items are transformed sequentially in enumeration order.
    /// </remarks>
#pragma warning disable MA0016
    public static async ValueTask<Dictionary<TKey, TResult>> SelectAsync<TKey, TValue, TResult>(
#pragma warning restore MA0016
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
