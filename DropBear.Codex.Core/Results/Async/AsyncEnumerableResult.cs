#region

using System.Collections.Frozen;
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
    public new static AsyncEnumerableResult<T, TError> Failure(TError error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new AsyncEnumerableResult<T, TError>(null, ResultState.Failure, error, exception);
    }

    /// <summary>
    ///     Creates a cancelled AsyncEnumerableResult.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static AsyncEnumerableResult<T, TError> Cancelled(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new AsyncEnumerableResult<T, TError>(null, ResultState.Cancelled, error);
    }

    #endregion

    #region Enumeration

    /// <summary>
    ///     Gets the async enumerator for the enumerable.
    /// </summary>
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
    public ConfiguredCancelableAsyncEnumerable<T> WithCancellation(CancellationToken cancellationToken)
    {
        if (!IsSuccess || _enumerable is null)
        {
            return EmptyAsyncEnumerable().ConfigureAwait(false).WithCancellation(cancellationToken);
        }

        return _enumerable.ConfigureAwait(false).WithCancellation(cancellationToken);
    }

    /// <summary>
    ///     Returns an empty async enumerator.
    /// </summary>
    private static async IAsyncEnumerator<T> EmptyAsyncEnumerator(CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }

    /// <summary>
    ///     Returns an empty async enumerable.
    /// </summary>
    private static async IAsyncEnumerable<T> EmptyAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }

    #endregion

    #region LINQ-style Operations

    /// <summary>
    ///     Filters items based on a predicate.
    /// </summary>
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
    ///     Materializes the async enumerable to a list.
    /// </summary>
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

    #region Debugger Display

    protected override string DebuggerDisplay =>
        $"State = {State}, IsSuccess = {IsSuccess}, " +
        $"HasEnumerable = {_enumerable is not null}, " +
        $"Error = {Error?.Message ?? "null"}";

    #endregion
}
