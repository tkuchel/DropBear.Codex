#region

using System.Buffers;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Span-based extension methods for optimal performance.
///     Reduces allocations in hot paths.
/// </summary>
public static class SpanExtensions
{
    /// <summary>
    ///     Filters successful results using span-based operations.
    ///     Uses ArrayPool to minimize allocations.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of the result error.</typeparam>
    /// <param name="results">The span of results to filter.</param>
    /// <param name="destination">The destination span to write successful values to.</param>
    /// <returns>The number of successful values written to the destination span.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FilterSuccessValues<T, TError>(
        this ReadOnlySpan<Result<T, TError>> results,
        Span<T> destination)
        where TError : ResultError
    {
        var count = 0;

        for (var i = 0; i < results.Length && count < destination.Length; i++)
        {
            ref readonly var result = ref results[i];
            if (result is { IsSuccess: true, Value: not null })
            {
                destination[count++] = result.Value;
            }
        }

        return count;
    }

    /// <summary>
    ///     Filters errors from results into a destination span.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of the result error.</typeparam>
    /// <param name="results">The span of results to filter.</param>
    /// <param name="destination">The destination span to write errors to.</param>
    /// <returns>The number of errors written to the destination span.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FilterErrors<T, TError>(
        this ReadOnlySpan<Result<T, TError>> results, // ADD 'this' keyword
        Span<TError> destination)
        where TError : ResultError
    {
        var count = 0;

        for (var i = 0; i < results.Length && count < destination.Length; i++)
        {
            ref readonly var result = ref results[i];
            if (result is { IsSuccess: false, Error: not null })
            {
                destination[count++] = result.Error;
            }
        }

        return count;
    }

    /// <summary>
    ///     Counts successful results without allocation.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of the result error.</typeparam>
    /// <param name="results">The span of results to count.</param>
    /// <returns>The number of successful results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountSuccesses<T, TError>(
        this ReadOnlySpan<Result<T, TError>> results) // ADD 'this' keyword
        where TError : ResultError
    {
        var count = 0;

        foreach (var t in results)
        {
            if (t.IsSuccess)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    ///     Counts failures without allocation.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of the result error.</typeparam>
    /// <param name="results">The span of results to count.</param>
    /// <returns>The number of failed results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountFailures<T, TError>(
        this ReadOnlySpan<Result<T, TError>> results) // ADD 'this' keyword
        where TError : ResultError
    {
        var count = 0;

        foreach (var t in results)
        {
            if (!t.IsSuccess)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    ///     Checks if all results are successful.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of the result error.</typeparam>
    /// <param name="results">The span of results to check.</param>
    /// <returns>True if all results are successful; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AllSuccess<T, TError>(
        this ReadOnlySpan<Result<T, TError>> results)
        where TError : ResultError
    {
        foreach (var t in results)
        {
            if (!t.IsSuccess)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Checks if any result is successful.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of the result error.</typeparam>
    /// <param name="results">The span of results to check.</param>
    /// <returns>True if any result is successful; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AnySuccess<T, TError>(
        this ReadOnlySpan<Result<T, TError>> results)
        where TError : ResultError
    {
        foreach (var t in results)
        {
            if (t.IsSuccess)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Partitions results using rented arrays from ArrayPool.
    ///     Returns rented arrays that must be returned to pool after use.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of the result error.</typeparam>
    /// <param name="results">The span of results to partition.</param>
    /// <returns>
    ///     A tuple containing the rented arrays of successes and errors,
    ///     along with their respective counts. Arrays must be returned to the pool using
    ///     <see cref="ReturnPooledArrays{T,TError}"/>.
    /// </returns>
    public static (T[] Successes, int SuccessCount, TError[] Errors, int ErrorCount) PartitionPooled<T, TError>(
        this ReadOnlySpan<Result<T, TError>> results) // ADD 'this' keyword
        where TError : ResultError
    {
        var successArray = ArrayPool<T>.Shared.Rent(results.Length);
        var errorArray = ArrayPool<TError>.Shared.Rent(results.Length);

        var successCount = 0;
        var errorCount = 0;

        foreach (var t in results)
        {
            ref readonly var result = ref t;

            if (result is { IsSuccess: true, Value: not null })
            {
                successArray[successCount++] = result.Value;
            }
            else if (result.Error is not null)
            {
                errorArray[errorCount++] = result.Error;
            }
        }

        return (successArray, successCount, errorArray, errorCount);
    }

    /// <summary>
    ///     Returns rented arrays to the pool.
    ///     Helper method to ensure proper cleanup.
    /// </summary>
    /// <typeparam name="T">The type of the success array elements.</typeparam>
    /// <typeparam name="TError">The type of the error array elements.</typeparam>
    /// <param name="successes">The rented array of successful values to return to the pool.</param>
    /// <param name="errors">The rented array of errors to return to the pool.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReturnPooledArrays<T, TError>(T[] successes, TError[] errors)
    {
        ArrayPool<T>.Shared.Return(successes, true);
        ArrayPool<TError>.Shared.Return(errors, true);
    }

    /// <summary>
    ///     Applies a transformation using pooled intermediate storage.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of the result error.</typeparam>
    /// <param name="results">The span of results to transform in place.</param>
    /// <param name="transformer">The function to apply to each successful result value.</param>
    public static void TransformInPlace<T, TError>(
        Span<Result<T, TError>> results, // This one should NOT have 'this' - it mutates
        Func<T, T> transformer)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(transformer);

        for (var i = 0; i < results.Length; i++)
        {
            ref var result = ref results[i];
            if (result is { IsSuccess: true, Value: not null })
            {
                var transformed = transformer(result.Value);
                result = Result<T, TError>.Success(transformed);
            }
        }
    }
}
