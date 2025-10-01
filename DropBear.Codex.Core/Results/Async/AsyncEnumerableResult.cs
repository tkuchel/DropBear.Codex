#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Attributes;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Async;

/// <summary>
///     Represents a Result that contains an IAsyncEnumerable value.
///     Provides unified error handling for asynchronous sequences.
///     Optimized for .NET 9 with improved cancellation and memory efficiency.
/// </summary>
/// <typeparam name="T">Type of the items in the enumerable.</typeparam>
/// <typeparam name="TError">Type of error that may occur.</typeparam>
[AsyncEnumerableResult(typeof(IAsyncEnumerable<>))]
[DebuggerDisplay("IsSuccess = {IsSuccess}, HasValue = {_enumerable != null}")]
public sealed class AsyncEnumerableResult<T, TError> : Result<IAsyncEnumerable<T>, TError>, IAsyncEnumerableResult<T>
    where TError : ResultError
{
    private readonly IAsyncEnumerable<T>? _enumerable;

    // Lazy-computed count (cached after first call)
    private int? _cachedCount;
    private readonly object _countLock = new();

    #region Constructors

    private AsyncEnumerableResult(
        IAsyncEnumerable<T> enumerable,
        ResultState state,
        TError? error = null,
        Exception? exception = null)
        : base(enumerable, state, error, exception)
    {
        _enumerable = enumerable;
    }

    #endregion

    #region IAsyncEnumerableResult Implementation

    public IReadOnlyCollection<Exception> Exceptions { get; }

    /// <inheritdoc />
    public async ValueTask<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        // Return cached count if available
        if (_cachedCount.HasValue)
        {
            return _cachedCount.Value;
        }

        if (!IsSuccess || _enumerable == null)
        {
            return 0;
        }

        // Lock to prevent multiple simultaneous counting
        lock (_countLock)
        {
            if (_cachedCount.HasValue)
            {
                return _cachedCount.Value;
            }
        }

        var count = 0;
        await foreach (var _ in _enumerable.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            count++;
        }

        lock (_countLock)
        {
            _cachedCount = count;
        }

        return count;
    }

    /// <inheritdoc />
    public async ValueTask<bool> HasItemsAsync(CancellationToken cancellationToken = default)
    {
        // Use cached count if available
        if (_cachedCount.HasValue)
        {
            return _cachedCount.Value > 0;
        }

        if (!IsSuccess || _enumerable == null)
        {
            return false;
        }

        // Check for at least one item
        await foreach (var _ in _enumerable.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Cache that we have at least one item
            lock (_countLock)
            {
                _cachedCount ??= 1;
            }

            return true;
        }

        // No items found
        lock (_countLock)
        {
            _cachedCount = 0;
        }

        return false;
    }

    /// <inheritdoc />
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!IsSuccess || _enumerable == null)
        {
            return EmptyAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
        }

        return _enumerable.GetAsyncEnumerator(cancellationToken);
    }

    #endregion

    #region Materialization Methods

    /// <summary>
    ///     Materializes the async enumerable into a list.
    /// </summary>
    public async ValueTask<Result<List<T>, TError>> ToListAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsSuccess)
        {
            return Result<List<T>, TError>.Failure(Error!);
        }

        try
        {
            var list = new List<T>();
            await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                list.Add(item);
            }

            // Cache the count
            lock (_countLock)
            {
                _cachedCount = list.Count;
            }

            return Result<List<T>, TError>.Success(list);
        }
        catch (OperationCanceledException)
        {
            var error = ResultError.CreateCancellationWithMessage<TError>();
            return Result<List<T>, TError>.Cancelled(error);
        }
        catch (Exception ex)
        {
            return Result<List<T>, TError>.Failure(
                (TError)Activator.CreateInstance(typeof(TError), ex.Message)!,
                ex);
        }
    }

    /// <summary>
    ///     Materializes the async enumerable into an array.
    /// </summary>
    public async ValueTask<Result<T[], TError>> ToArrayAsync(
        CancellationToken cancellationToken = default)
    {
        var listResult = await ToListAsync(cancellationToken).ConfigureAwait(false);
        return listResult.IsSuccess
            ? Result<T[], TError>.Success(listResult.Value!.ToArray())
            : Result<T[], TError>.Failure(listResult.Error!);
    }

    /// <summary>
    ///     Gets the first item or a failure if empty.
    /// </summary>
    public async ValueTask<Result<T, TError>> FirstOrFailureAsync(
        TError emptyError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emptyError);

        if (!IsSuccess)
        {
            return Result<T, TError>.Failure(Error!);
        }

        await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            return Result<T, TError>.Success(item);
        }

        return Result<T, TError>.Failure(emptyError);
    }

    /// <summary>
    ///     Gets the first item matching the predicate or a failure if none found.
    /// </summary>
    public async ValueTask<Result<T, TError>> FirstOrFailureAsync(
        Func<T, bool> predicate,
        TError emptyError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(emptyError);

        if (!IsSuccess)
        {
            return Result<T, TError>.Failure(Error!);
        }

        await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            if (predicate(item))
            {
                return Result<T, TError>.Success(item);
            }
        }

        return Result<T, TError>.Failure(emptyError);
    }

    #endregion

    #region Aggregation Methods

    /// <summary>
    ///     Aggregates the async enumerable using the specified function.
    /// </summary>
    public async ValueTask<Result<TAccumulate, TError>> AggregateAsync<TAccumulate>(
        TAccumulate seed,
        Func<TAccumulate, T, TAccumulate> aggregator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregator);

        if (!IsSuccess)
        {
            return Result<TAccumulate, TError>.Failure(Error!);
        }

        try
        {
            var accumulator = seed;
            await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                accumulator = aggregator(accumulator, item);
            }

            return Result<TAccumulate, TError>.Success(accumulator);
        }
        catch (OperationCanceledException)
        {
            var error = ResultError.CreateCancellationWithMessage<TError>();
            return Result<TAccumulate, TError>.Cancelled(error);
        }
        catch (Exception ex)
        {
            return Result<TAccumulate, TError>.Failure(
                (TError)Activator.CreateInstance(typeof(TError), ex.Message)!,
                ex);
        }
    }

    /// <summary>
    ///     Checks if any items match the predicate.
    /// </summary>
    public async ValueTask<bool> AnyAsync(
        Func<T, bool>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSuccess || _enumerable == null)
        {
            return false;
        }

        if (predicate == null)
        {
            return await HasItemsAsync(cancellationToken).ConfigureAwait(false);
        }

        await foreach (var item in _enumerable.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            if (predicate(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Checks if all items match the predicate.
    /// </summary>
    public async ValueTask<bool> AllAsync(
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (!IsSuccess || _enumerable == null)
        {
            return false;
        }

        await foreach (var item in _enumerable.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            if (!predicate(item))
            {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Transformation Methods

    /// <summary>
    ///     Transforms each item in the async enumerable.
    /// </summary>
    public AsyncEnumerableResult<TResult, TError> Select<TResult>(Func<T, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (!IsSuccess)
        {
            return AsyncEnumerableResult<TResult, TError>.Failure(Error!);
        }

        async IAsyncEnumerable<TResult> SelectAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                yield return selector(item);
            }
        }

        return AsyncEnumerableResult<TResult, TError>.Success(SelectAsync());
    }

    /// <summary>
    ///     Transforms each item asynchronously.
    /// </summary>
    public AsyncEnumerableResult<TResult, TError> SelectAsync<TResult>(
        Func<T, ValueTask<TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (!IsSuccess)
        {
            return AsyncEnumerableResult<TResult, TError>.Failure(Error!);
        }

        async IAsyncEnumerable<TResult> SelectAsyncEnumerable(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                yield return await selector(item).ConfigureAwait(false);
            }
        }

        return AsyncEnumerableResult<TResult, TError>.Success(SelectAsyncEnumerable());
    }

    /// <summary>
    ///     Filters items based on a predicate.
    /// </summary>
    public AsyncEnumerableResult<T, TError> Where(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (!IsSuccess)
        {
            return this;
        }

        async IAsyncEnumerable<T> WhereAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                if (predicate(item))
                {
                    yield return item;
                }
            }
        }

        return AsyncEnumerableResult<T, TError>.Success(WhereAsync());
    }

    /// <summary>
    ///     Filters items asynchronously based on a predicate.
    /// </summary>
    public AsyncEnumerableResult<T, TError> WhereAsync(Func<T, ValueTask<bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (!IsSuccess)
        {
            return this;
        }

        async IAsyncEnumerable<T> WhereAsyncEnumerable(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                if (await predicate(item).ConfigureAwait(false))
                {
                    yield return item;
                }
            }
        }

        return AsyncEnumerableResult<T, TError>.Success(WhereAsyncEnumerable());
    }

    /// <summary>
    ///     Takes the first N items.
    /// </summary>
    public AsyncEnumerableResult<T, TError> Take(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        if (!IsSuccess)
        {
            return this;
        }

        async IAsyncEnumerable<T> TakeAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var taken = 0;
            await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                if (taken >= count)
                {
                    yield break;
                }

                yield return item;
                taken++;
            }
        }

        return AsyncEnumerableResult<T, TError>.Success(TakeAsync());
    }

    /// <summary>
    ///     Skips the first N items.
    /// </summary>
    public AsyncEnumerableResult<T, TError> Skip(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (!IsSuccess)
        {
            return this;
        }

        if (count == 0)
        {
            return this;
        }

        async IAsyncEnumerable<T> SkipAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var skipped = 0;
            await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                if (skipped < count)
                {
                    skipped++;
                    continue;
                }

                yield return item;
            }
        }

        return AsyncEnumerableResult<T, TError>.Success(SkipAsync());
    }

    /// <summary>
    ///     Batches items into groups of the specified size.
    /// </summary>
    public AsyncEnumerableResult<IReadOnlyList<T>, TError> Batch(int batchSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

        if (!IsSuccess)
        {
            return AsyncEnumerableResult<IReadOnlyList<T>, TError>.Failure(Error!);
        }

        async IAsyncEnumerable<IReadOnlyList<T>> BatchAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var batch = new List<T>(batchSize);

            await foreach (var item in _enumerable!.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                batch.Add(item);

                if (batch.Count == batchSize)
                {
                    yield return batch.AsReadOnly();
                    batch = new List<T>(batchSize);
                }
            }

            if (batch.Count > 0)
            {
                yield return batch.AsReadOnly();
            }
        }

        return AsyncEnumerableResult<IReadOnlyList<T>, TError>.Success(BatchAsync());
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Creates an empty async enumerable.
    /// </summary>
    private static async IAsyncEnumerable<T> EmptyAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
        yield break;
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a new successful AsyncEnumerableResult.
    /// </summary>
    public new static AsyncEnumerableResult<T, TError> Success(IAsyncEnumerable<T> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new AsyncEnumerableResult<T, TError>(value, ResultState.Success);
    }

    /// <summary>
    ///     Creates a new failed AsyncEnumerableResult.
    /// </summary>
    public new static AsyncEnumerableResult<T, TError> Failure(TError error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new AsyncEnumerableResult<T, TError>(EmptyAsyncEnumerable(), ResultState.Failure, error, exception);
    }

    /// <summary>
    ///     Creates a new warning AsyncEnumerableResult.
    /// </summary>
    public new static AsyncEnumerableResult<T, TError> Warning(IAsyncEnumerable<T> value, TError error)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(error);
        return new AsyncEnumerableResult<T, TError>(value, ResultState.Warning, error);
    }

    /// <summary>
    ///     Creates a new partial success AsyncEnumerableResult.
    /// </summary>
    public new static AsyncEnumerableResult<T, TError> PartialSuccess(IAsyncEnumerable<T> value, TError error)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(error);
        return new AsyncEnumerableResult<T, TError>(value, ResultState.PartialSuccess, error);
    }

    /// <summary>
    ///     Creates a new cancelled AsyncEnumerableResult.
    /// </summary>
    public new static AsyncEnumerableResult<T, TError> Cancelled(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new AsyncEnumerableResult<T, TError>(EmptyAsyncEnumerable(), ResultState.Cancelled, error);
    }

    /// <summary>
    ///     Creates a new pending AsyncEnumerableResult.
    /// </summary>
    public new static AsyncEnumerableResult<T, TError> Pending(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new AsyncEnumerableResult<T, TError>(EmptyAsyncEnumerable(), ResultState.Pending, error);
    }

    /// <summary>
    ///     Creates a new NoOp AsyncEnumerableResult.
    /// </summary>
    public new static AsyncEnumerableResult<T, TError> NoOp(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new AsyncEnumerableResult<T, TError>(EmptyAsyncEnumerable(), ResultState.NoOp, error);
    }

    #endregion
}
