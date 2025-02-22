namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Provides a custom async enumerable pattern for results.
/// </summary>
public interface IAsyncEnumerableResult<T> : IAsyncEnumerable<T>
{
    ValueTask<int> GetCountAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> HasItemsAsync(CancellationToken cancellationToken = default);
}
