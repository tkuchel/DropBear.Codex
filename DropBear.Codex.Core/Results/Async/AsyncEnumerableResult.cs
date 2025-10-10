#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Async;

/// <summary>
///     Represents a result that wraps an asynchronous enumerable.
///     Optimized for .NET 9 with modern async patterns.
/// </summary>
/// <typeparam name="T">The type of items in the async enumerable.</typeparam>
/// <typeparam name="TError">The error type.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class AsyncEnumerableResult<T, TError> : Result<TError>
    where TError : ResultError
{
    private readonly IAsyncEnumerable<T>? _enumerable;

    /// <summary>
    ///     Initializes a new instance of AsyncEnumerableResult.
    /// </summary>
    private AsyncEnumerableResult(
        IAsyncEnumerable<T>? enumerable,
        ResultState state,
        TError? error = null,
        Exception? exception = null)
        : base(state, error, exception)
    {
        _enumerable = enumerable;
        Exceptions = exception is not null ? [exception] : [];
    }

    /// <summary>
    ///     Gets the exceptions collection. Required by base class.
    /// </summary>
    public new IReadOnlyCollection<Exception> Exceptions { get; }

    #region Debugger Display

    /// <inheritdoc />
    protected override string DebuggerDisplay =>
        $"State = {State}, IsSuccess = {IsSuccess}, " +
        $"HasEnumerable = {_enumerable is not null}, " +
        $"Error = {Error?.Message ?? "null"}";

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Creates a cancellation error.
    /// </summary>
    private static TError CreateCancellationError()
    {
        return (TError)Activator.CreateInstance(
            typeof(TError),
            "Operation was cancelled")!;
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a successful AsyncEnumerableResult.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncEnumerableResult<T, TError> Success(IAsyncEnumerable<T> enumerable)
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        return new AsyncEnumerableResult<T, TError>(enumerable, ResultState.Success);
    }

    /// <summary>
    ///     Creates a failed AsyncEnumerableResult.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static new AsyncEnumerableResult<T, TError> Failure(TError error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new AsyncEnumerableResult<T, TError>(null, ResultState.Failure, error, exception);
    }

    /// <summary>
    ///     Creates a cancelled AsyncEnumerableResult.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static new AsyncEnumerableResult<T, TError> Cancelled(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new AsyncEnumerableResult<T, TError>(null, ResultState.Cancelled, error);
    }

    #endregion

    #region Enumeration

    /// <summary>
    ///     Gets the async enumerator for the enumerable.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token for the enumeration.</param>
    /// <returns>An async enumerator for the enumerable, or an empty enumerator if the result is not successful.</returns>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!IsSuccess || _enumerable is null)
        {
            return EmptyAsyncEnumerator(cancellationToken);
        }

        return _enumerable.GetAsyncEnumerator(cancellationToken);
    }

    /// <summary>
    ///     Configures the async enumerable with cancellation token.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>A configured cancelable async enumerable.</returns>
    public ConfiguredCancelableAsyncEnumerable<T> WithCancellation(CancellationToken cancellationToken)
    {
        if (!IsSuccess || _enumerable is null)
        {
            return EmptyAsyncEnumerable().ConfigureAwait(false).WithCancellation(cancellationToken);
        }

        return _enumerable.ConfigureAwait(false).WithCancellation(cancellationToken);
    }

    /// <summary>
    ///     Returns an empty async enumerator that respects cancellation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>An empty async enumerator.</returns>
    /// <remarks>
    ///     This method checks cancellation before yielding nothing, ensuring proper cancellation behavior.
    /// </remarks>
    private static async IAsyncEnumerator<T> EmptyAsyncEnumerator(CancellationToken cancellationToken)
    {
        // Check cancellation even though we're yielding nothing - this ensures
        // proper cancellation semantics if the token was already cancelled
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }

    /// <summary>
    ///     Returns an empty async enumerable that respects cancellation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>An empty async enumerable.</returns>
    /// <remarks>
    ///     This method checks cancellation before yielding nothing, ensuring proper cancellation behavior.
    /// </remarks>
    private static async IAsyncEnumerable<T> EmptyAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Check cancellation even though we're yielding nothing - this ensures
        // proper cancellation semantics if the token was already cancelled
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }

    #endregion

    #region LINQ-style Operations

    /// <summary>
    ///     Filters items based on a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to filter items.</param>
    /// <returns>A new AsyncEnumerableResult containing only items that match the predicate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when predicate is null.</exception>
    public AsyncEnumerableResult<T, TError> Where(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (!IsSuccess || _enumerable is null)
        {
            return this;
        }

        async IAsyncEnumerable<T> FilteredEnumerable(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in _enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (predicate(item))
                {
                    yield return item;
                }
            }
        }

        return Success(FilteredEnumerable());
    }

    /// <summary>
    ///     Filters items asynchronously based on a predicate.
    /// </summary>
    /// <param name="predicateAsync">The async predicate to filter items.</param>
    /// <returns>A new AsyncEnumerableResult containing only items that match the predicate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when predicateAsync is null.</exception>
    public AsyncEnumerableResult<T, TError> WhereAsync(Func<T, ValueTask<bool>> predicateAsync)
    {
        ArgumentNullException.ThrowIfNull(predicateAsync);

        if (!IsSuccess || _enumerable is null)
        {
            return this;
        }

        async IAsyncEnumerable<T> FilteredEnumerableAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in _enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (await predicateAsync(item).ConfigureAwait(false))
                {
                    yield return item;
                }
            }
        }

        return Success(FilteredEnumerableAsync());
    }

    /// <summary>
    ///     Projects each item to a new form.
    /// </summary>
    /// <typeparam name="TResult">The type of the result items.</typeparam>
    /// <param name="selector">The transformation function.</param>
    /// <returns>A new AsyncEnumerableResult containing the transformed items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when selector is null.</exception>
    public AsyncEnumerableResult<TResult, TError> Select<TResult>(Func<T, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (!IsSuccess || _enumerable is null)
        {
            return AsyncEnumerableResult<TResult, TError>.Failure(Error!);
        }

        async IAsyncEnumerable<TResult> MappedEnumerable(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in _enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return selector(item);
            }
        }

        return AsyncEnumerableResult<TResult, TError>.Success(MappedEnumerable());
    }

    /// <summary>
    ///     Projects each item to a new form asynchronously.
    /// </summary>
    /// <typeparam name="TResult">The type of the result items.</typeparam>
    /// <param name="selectorAsync">The async transformation function.</param>
    /// <returns>A new AsyncEnumerableResult containing the transformed items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when selectorAsync is null.</exception>
    public AsyncEnumerableResult<TResult, TError> SelectAsync<TResult>(
        Func<T, ValueTask<TResult>> selectorAsync)
    {
        ArgumentNullException.ThrowIfNull(selectorAsync);

        if (!IsSuccess || _enumerable is null)
        {
            return AsyncEnumerableResult<TResult, TError>.Failure(Error!);
        }

        async IAsyncEnumerable<TResult> MappedEnumerableAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in _enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return await selectorAsync(item).ConfigureAwait(false);
            }
        }

        return AsyncEnumerableResult<TResult, TError>.Success(MappedEnumerableAsync());
    }

    /// <summary>
    ///     Takes the first N items.
    /// </summary>
    /// <param name="count">The number of items to take.</param>
    /// <returns>A new AsyncEnumerableResult containing at most the first N items.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when count is negative.</exception>
    public AsyncEnumerableResult<T, TError> Take(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative");
        }

        if (!IsSuccess || _enumerable is null)
        {
            return this;
        }

        async IAsyncEnumerable<T> TakenEnumerable(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var taken = 0;
            await foreach (var item in _enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (taken >= count)
                {
                    yield break;
                }

                yield return item;
                taken++;
            }
        }

        return Success(TakenEnumerable());
    }

    /// <summary>
    ///     Skips the first N items.
    /// </summary>
    /// <param name="count">The number of items to skip.</param>
    /// <returns>A new AsyncEnumerableResult containing all items after the first N.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when count is negative.</exception>
    public AsyncEnumerableResult<T, TError> Skip(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative");
        }

        if (!IsSuccess || _enumerable is null)
        {
            return this;
        }

        async IAsyncEnumerable<T> SkippedEnumerable(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var skipped = 0;
            await foreach (var item in _enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (skipped < count)
                {
                    skipped++;
                    continue;
                }

                yield return item;
            }
        }

        return Success(SkippedEnumerable());
    }

    #endregion

    #region Materialization

    /// <summary>
    ///     Materializes the async enumerable to a read-only list.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    ///     A Result containing a read-only list of all items, or a failure/cancelled result.
    /// </returns>
    /// <remarks>
    ///     This method enumerates the entire async enumerable and stores all items in memory.
    ///     For large sequences, consider using streaming operations instead.
    /// </remarks>
    public async ValueTask<Result<IReadOnlyList<T>, TError>> ToListAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsSuccess || _enumerable is null)
        {
            return Result<IReadOnlyList<T>, TError>.Failure(Error!);
        }

        try
        {
            var list = new List<T>();

            await foreach (var item in _enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                list.Add(item);
            }

            return Result<IReadOnlyList<T>, TError>.Success(list.AsReadOnly());
        }
        catch (OperationCanceledException)
        {
            var cancelledError = CreateCancellationError();
            return Result<IReadOnlyList<T>, TError>.Cancelled(cancelledError);
        }
        catch (Exception ex)
        {
            var error = (TError)Activator.CreateInstance(
                typeof(TError),
                $"Failed to materialize async enumerable: {ex.Message}")!;
            return Result<IReadOnlyList<T>, TError>.Failure(error, ex);
        }
    }

    /// <summary>
    ///     Materializes the async enumerable to an array.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    ///     A Result containing an array of all items, or a failure/cancelled result.
    /// </returns>
    /// <remarks>
    ///     This method enumerates the entire async enumerable and stores all items in memory.
    ///     For large sequences, consider using streaming operations instead.
    /// </remarks>
    public async ValueTask<Result<T[], TError>> ToArrayAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsSuccess || _enumerable is null)
        {
            return Result<T[], TError>.Failure(Error!);
        }

        try
        {
            var list = new List<T>();

            await foreach (var item in _enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                list.Add(item);
            }

            return Result<T[], TError>.Success(list.ToArray());
        }
        catch (OperationCanceledException)
        {
            var cancelledError = CreateCancellationError();
            return Result<T[], TError>.Cancelled(cancelledError);
        }
        catch (Exception ex)
        {
            var error = (TError)Activator.CreateInstance(
                typeof(TError),
                $"Failed to materialize async enumerable: {ex.Message}")!;
            return Result<T[], TError>.Failure(error, ex);
        }
    }

    /// <summary>
    ///     Counts the number of items in the async enumerable.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    ///     A Result containing the count of items, or a failure/cancelled result.
    /// </returns>
    /// <remarks>
    ///     This method enumerates the entire async enumerable to count items.
    ///     The enumeration stops if cancellation is requested.
    /// </remarks>
    public async ValueTask<Result<int, TError>> CountAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsSuccess || _enumerable is null)
        {
            return Result<int, TError>.Failure(Error!);
        }

        try
        {
            var count = 0;

            await foreach (var _ in _enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                count++;
            }

            return Result<int, TError>.Success(count);
        }
        catch (OperationCanceledException)
        {
            var cancelledError = CreateCancellationError();
            return Result<int, TError>.Cancelled(cancelledError);
        }
        catch (Exception ex)
        {
            var error = (TError)Activator.CreateInstance(
                typeof(TError),
                $"Failed to count async enumerable: {ex.Message}")!;
            return Result<int, TError>.Failure(error, ex);
        }
    }

    #endregion
}
