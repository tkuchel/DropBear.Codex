using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Attributes;
using DropBear.Codex.Core.Results.Base;

namespace DropBear.Codex.Core.Results.Async;

/// <summary>
///     Represents a Result that contains an IAsyncEnumerable value.
///     Provides a unified way to handle asynchronous sequences with error handling.
/// </summary>
/// <typeparam name="T">Type of the items in the enumerable.</typeparam>
/// <typeparam name="TError">Type of error that may occur.</typeparam>
[AsyncEnumerableResult(typeof(IAsyncEnumerable<>))]
public sealed class AsyncEnumerableResult<T, TError> : Result<IAsyncEnumerable<T>, TError>, IAsyncEnumerableResult<T>
    where TError : ResultError
{
    // The underlying async enumerable
    private readonly IAsyncEnumerable<T>? _enumerable;
    // Cached count for optimization
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

    #region IAsyncEnumerableResult Implementation

    /// <summary>
    ///     Gets the count of items in the enumerable.
    /// </summary>
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

    /// <summary>
    ///     Checks if the enumerable has any items.
    /// </summary>
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

    /// <summary>
    ///     Gets the async enumerator for the underlying sequence.
    /// </summary>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!IsSuccess || _enumerable == null)
        {
            return Empty().GetAsyncEnumerator(cancellationToken);
        }

        return EnumerateAsync(cancellationToken);
    }

    #endregion

    #region Private Helpers

    /// <summary>
    ///     Provides the enumerator that iterates through the underlying enumerable.
    /// </summary>
    private async IAsyncEnumerator<T> EnumerateAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    /// <summary>
    ///     Creates an empty async enumerable for use when the result is a failure.
    /// </summary>
    private static async IAsyncEnumerable<T> Empty([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }

        await Task.CompletedTask.ConfigureAwait(false); // Make the method truly async
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a new successful AsyncEnumerableResult with the specified enumerable.
    /// </summary>
    /// <param name="value">The async enumerable value.</param>
    /// <returns>A successful result containing the async enumerable.</returns>
    public new static AsyncEnumerableResult<T, TError> Success(IAsyncEnumerable<T> value)
    {
        return new AsyncEnumerableResult<T, TError>(value, ResultState.Success);
    }

    /// <summary>
    ///     Creates a new failed AsyncEnumerableResult with the specified error.
    /// </summary>
    /// <param name="error">The error that caused the failure.</param>
    /// <param name="exception">Optional exception associated with the failure.</param>
    /// <returns>A failed result.</returns>
    public new static AsyncEnumerableResult<T, TError> Failure(TError error, Exception? exception = null)
    {
        return new AsyncEnumerableResult<T, TError>(Empty(), ResultState.Failure, error, exception);
    }

    /// <summary>
    ///     Creates a new warning AsyncEnumerableResult with the specified enumerable and error.
    /// </summary>
    /// <param name="value">The async enumerable value.</param>
    /// <param name="error">The warning information.</param>
    /// <returns>A result in the warning state.</returns>
    public new static AsyncEnumerableResult<T, TError> Warning(IAsyncEnumerable<T> value, TError error)
    {
        return new AsyncEnumerableResult<T, TError>(value, ResultState.Warning, error);
    }

    /// <summary>
    ///     Creates a new partial success AsyncEnumerableResult with the specified enumerable and error.
    /// </summary>
    /// <param name="value">The async enumerable value.</param>
    /// <param name="error">Information about the partial success condition.</param>
    /// <returns>A result in the partial success state.</returns>
    public new static AsyncEnumerableResult<T, TError> PartialSuccess(IAsyncEnumerable<T> value, TError error)
    {
        return new AsyncEnumerableResult<T, TError>(value, ResultState.PartialSuccess, error);
    }

    /// <summary>
    ///     Creates a new cancelled AsyncEnumerableResult with the specified error.
    /// </summary>
    /// <param name="error">Information about the cancellation.</param>
    /// <returns>A result in the cancelled state.</returns>
    public new static AsyncEnumerableResult<T, TError> Cancelled(TError error)
    {
        return new AsyncEnumerableResult<T, TError>(Empty(), ResultState.Cancelled, error);
    }

    /// <summary>
    ///     Creates a new cancelled AsyncEnumerableResult with partial results and an error.
    /// </summary>
    /// <param name="value">The partial async enumerable results before cancellation.</param>
    /// <param name="error">Information about the cancellation.</param>
    /// <returns>A result in the cancelled state with partial results.</returns>
    public new static AsyncEnumerableResult<T, TError> Cancelled(IAsyncEnumerable<T> value, TError error)
    {
        return new AsyncEnumerableResult<T, TError>(value, ResultState.Cancelled, error);
    }

    /// <summary>
    ///     Creates a new pending AsyncEnumerableResult with the specified error.
    /// </summary>
    /// <param name="error">Information about the pending operation.</param>
    /// <returns>A result in the pending state.</returns>
    public new static AsyncEnumerableResult<T, TError> Pending(TError error)
    {
        return new AsyncEnumerableResult<T, TError>(Empty(), ResultState.Pending, error);
    }

    /// <summary>
    ///     Creates a new pending AsyncEnumerableResult with interim results and an error.
    /// </summary>
    /// <param name="value">Interim async enumerable results while the operation is pending.</param>
    /// <param name="error">Information about the pending operation.</param>
    /// <returns>A result in the pending state with interim results.</returns>
    public new static AsyncEnumerableResult<T, TError> Pending(IAsyncEnumerable<T> value, TError error)
    {
        return new AsyncEnumerableResult<T, TError>(value, ResultState.Pending, error);
    }

    /// <summary>
    ///     Creates a new NoOp AsyncEnumerableResult with the specified error.
    /// </summary>
    /// <param name="error">Information about why no operation was performed.</param>
    /// <returns>A result in the NoOp state.</returns>
    public new static AsyncEnumerableResult<T, TError> NoOp(TError error)
    {
        return new AsyncEnumerableResult<T, TError>(Empty(), ResultState.NoOp, error);
    }

    /// <summary>
    ///     Creates a new NoOp AsyncEnumerableResult with context data and an error.
    /// </summary>
    /// <param name="value">Context async enumerable data related to the no-op condition.</param>
    /// <param name="error">Information about why no operation was performed.</param>
    /// <returns>A result in the NoOp state with context data.</returns>
    public new static AsyncEnumerableResult<T, TError> NoOp(IAsyncEnumerable<T> value, TError error)
    {
        return new AsyncEnumerableResult<T, TError>(value, ResultState.NoOp, error);
    }

    #endregion
}
