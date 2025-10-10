#region

using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Provides a custom async enumerable pattern for results.
/// </summary>
public interface IAsyncEnumerableResult<out T> : IAsyncEnumerable<T>
{
    /// <summary>
    ///     Gets the <see cref="ResultState" /> indicating the success or failure status of this result.
    /// </summary>
    ResultState State { get; }

    /// <summary>
    ///     Gets a value indicating whether this result represents a successful operation.
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    ///     Gets an optional exception if the result represents a failure that threw an exception.
    /// </summary>
    Exception? Exception { get; }

    /// <summary>
    ///     Gets a read-only collection of exceptions if multiple exceptions occurred.
    /// </summary>
    IReadOnlyCollection<Exception> Exceptions { get; }

    /// <summary>
    ///     Gets the count of items in the enumerable.
    /// </summary>
    ValueTask<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if the enumerable has any items.
    /// </summary>
    ValueTask<bool> HasItemsAsync(CancellationToken cancellationToken = default);
}
