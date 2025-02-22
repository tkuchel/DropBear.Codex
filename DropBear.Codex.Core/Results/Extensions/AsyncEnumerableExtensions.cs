using DropBear.Codex.Core.Results.Async;

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Extension methods for IAsyncEnumerable.
/// </summary>
public static class AsyncEnumerableExtensions
{
    public static IAsyncEnumerable<T> ConfigureAwait<T>(this IAsyncEnumerable<T> enumerable,
        bool continueOnCapturedContext)
    {
        return new ConfiguredAsyncEnumerable<T>(enumerable, continueOnCapturedContext);
    }
}
