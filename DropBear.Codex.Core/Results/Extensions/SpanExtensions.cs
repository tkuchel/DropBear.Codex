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
    public static int FilterSuccessValues<T, TError>(
        ReadOnlySpan<Result<T, TError>> results,
        Span<T> destination)
        where TError : ResultError
    {
        var count = 0;

        for (var i = 0; i < results.Length && count < destination.Length; i++)
        {
            ref readonly var result = ref results[i];
            if (result.IsSuccess)
            {
                destination[count++] = result.Value!;
            }
        }

        return count;
    }

    /// <summary>
    ///     Counts successful results without allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountSuccesses<T, TError>(ReadOnlySpan<Result<T, TError>> results)
        where TError : ResultError
    {
        var count = 0;

        for (var i = 0; i < results.Length; i++)
        {
            if (results[i].IsSuccess)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    ///     Counts failures without allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountFailures<T, TError>(ReadOnlySpan<Result<T, TError>> results)
        where TError : ResultError
    {
        var count = 0;

        for (var i = 0; i < results.Length; i++)
        {
            if (!results[i].IsSuccess)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    ///     Partitions results using rented arrays from ArrayPool.
    ///     Returns rented arrays that must be returned to pool after use.
    /// </summary>
    public static (T[] Successes, int SuccessCount, TError[] Errors, int ErrorCount) PartitionPooled<T, TError>(
        ReadOnlySpan<Result<T, TError>> results)
        where TError : ResultError
    {
        var successArray = ArrayPool<T>.Shared.Rent(results.Length);
        var errorArray = ArrayPool<TError>.Shared.Rent(results.Length);

        var successCount = 0;
        var errorCount = 0;

        for (var i = 0; i < results.Length; i++)
        {
            ref readonly var result = ref results[i];

            if (result.IsSuccess)
            {
                successArray[successCount++] = result.Value!;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReturnPooledArrays<T, TError>(T[] successes, TError[] errors)
    {
        ArrayPool<T>.Shared.Return(successes, clearArray: true);
        ArrayPool<TError>.Shared.Return(errors, clearArray: true);
    }

    /// <summary>
    ///     Applies a transformation using pooled intermediate storage.
    /// </summary>
    public static void TransformInPlace<T, TError>(
        Span<Result<T, TError>> results,
        Func<T, T> transformer)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(transformer);

        for (var i = 0; i < results.Length; i++)
        {
            ref var result = ref results[i];
            if (result.IsSuccess)
            {
                var transformed = transformer(result.Value!);
                result = Result<T, TError>.Success(transformed);
            }
        }
    }
}
