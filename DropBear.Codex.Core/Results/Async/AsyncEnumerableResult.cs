#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Attributes;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Async;

/// <summary>
///     Represents a Result that contains an IAsyncEnumerable value.
/// </summary>
[AsyncEnumerableResult(typeof(IAsyncEnumerable<>))]
public sealed class AsyncEnumerableResult<T, TError> : Result<IAsyncEnumerable<T>, TError>, IAsyncEnumerableResult<T>
    where TError : ResultError
{
    private readonly IAsyncEnumerable<T>? _enumerable;
    private int? _count;

    private AsyncEnumerableResult(
        IAsyncEnumerable<T> enumerable,
        ResultState state,
        TError? error = null,
        Exception? exception = null)
        : base(enumerable, state, error, exception)
    {
        _enumerable = enumerable;
    }

    public async ValueTask<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        if (_count.HasValue)
        {
            return _count.Value;
        }

        var count = 0;
        await foreach (var _ in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            count++;
        }

        _count = count;
        return count;
    }

    public async ValueTask<bool> HasItemsAsync(CancellationToken cancellationToken = default)
    {
        if (_count.HasValue)
        {
            return _count.Value > 0;
        }

        await foreach (var _ in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _count = 1;
            return true;
        }

        _count = 0;
        return false;
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!IsSuccess || _enumerable == null)
        {
            return EmptyAsyncEnumerator();
        }

        return EnumerateAsync(cancellationToken);
    }

    public new static AsyncEnumerableResult<T, TError> Success(IAsyncEnumerable<T> value)
    {
        return new AsyncEnumerableResult<T, TError>(value, ResultState.Success);
    }

    public new static AsyncEnumerableResult<T, TError> Failure(TError error, Exception? exception = null)
    {
        return new AsyncEnumerableResult<T, TError>(Empty(), ResultState.Failure, error, exception);
    }

    private async IAsyncEnumerator<T> EnumerateAsync(
        CancellationToken cancellationToken = default)
    {
        await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    private static IAsyncEnumerator<T> EmptyAsyncEnumerator()
    {
        return Empty().GetAsyncEnumerator();
    }

    private static async IAsyncEnumerable<T> Empty(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }

        await Task.CompletedTask.ConfigureAwait(false); // Add await to make the method truly async
    }
}
