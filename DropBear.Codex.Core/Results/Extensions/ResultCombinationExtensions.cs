#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides combination extension methods for Result types
/// </summary>
public static class ResultCombinationExtensions
{
    #region Tuple Combinations

    /// <summary>
    ///     Combines two Results into a tuple Result
    /// </summary>
    public static Result<(T1, T2), TError> Combine<T1, T2, TError>(
        this Result<T1, TError> result1,
        Result<T2, TError> result2)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result1);
        ArgumentNullException.ThrowIfNull(result2);

        if (result1.IsSuccess && result2.IsSuccess)
        {
            return Result<(T1, T2), TError>.Success((result1.Value!, result2.Value!));
        }

        var errors = new List<TError>(2);
        CollectError(result1, errors);
        CollectError(result2, errors);

        return Result<(T1, T2), TError>.Failure(
            CreateCompositeError(errors));
    }

    /// <summary>
    ///     Combines three Results into a tuple Result
    /// </summary>
    public static Result<(T1, T2, T3), TError> Combine<T1, T2, T3, TError>(
        this Result<T1, TError> result1,
        Result<T2, TError> result2,
        Result<T3, TError> result3)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result1);
        ArgumentNullException.ThrowIfNull(result2);
        ArgumentNullException.ThrowIfNull(result3);

        if (result1.IsSuccess && result2.IsSuccess && result3.IsSuccess)
        {
            return Result<(T1, T2, T3), TError>.Success((result1.Value!, result2.Value!, result3.Value!));
        }

        var errors = new List<TError>(3);
        CollectError(result1, errors);
        CollectError(result2, errors);
        CollectError(result3, errors);

        return Result<(T1, T2, T3), TError>.Failure(
            CreateCompositeError(errors));
    }

    #endregion

    #region Collection Operations

    /// <summary>
    ///     Converts a sequence of Results into a Result of sequence
    /// </summary>
    public static Result<IReadOnlyList<T>, TError> Sequence<T, TError>(
        this IEnumerable<Result<T, TError>> results)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.Traverse();
    }

    /// <summary>
    ///     Transforms a sequence of Results into a Result of sequence, collecting all errors
    /// </summary>
    public static Result<IReadOnlyList<T>, TError> Traverse<T, TError>(
        this IEnumerable<Result<T, TError>> results)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(results);

        var successes = new List<T>();
        var errors = new List<TError>();

        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                successes.Add(result.Value!);
            }
            else
            {
                CollectError(result, errors);
            }
        }

        if (errors.Count == 0)
        {
            return Result<IReadOnlyList<T>, TError>.Success(successes);
        }

        return successes.Count > 0
            ? Result<IReadOnlyList<T>, TError>.PartialSuccess(
                successes,
                CreateCompositeError(errors))
            : Result<IReadOnlyList<T>, TError>.Failure(
                CreateCompositeError(errors));
    }

    /// <summary>
    ///     Gets the first element of a Result sequence or returns a failure
    /// </summary>
    public static Result<T, TError> FirstOrFailure<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<TError> onEmpty)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onEmpty);

        return result.Bind(items =>
        {
            using var enumerator = items.GetEnumerator();
            return enumerator.MoveNext()
                ? Result<T, TError>.Success(enumerator.Current)
                : Result<T, TError>.Failure(onEmpty());
        });
    }

    /// <summary>
    ///     Gets the first element of a Result sequence or a default value
    /// </summary>
    public static Result<T, TError> FirstOrDefault<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        T defaultValue = default!)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Map(items => items.FirstOrDefault(defaultValue));
    }

    #endregion

    #region Helper Methods

    private static void CollectError<T, TError>(
        Result<T, TError> result,
        ICollection<TError> errors)
        where TError : ResultError
    {
        if (!result.IsSuccess && result.Error is not null)
        {
            errors.Add(result.Error);
        }
    }

    private static TError CreateCompositeError<TError>(IEnumerable<TError> errors)
        where TError : ResultError
    {
        var compositeError = new CompositeError(errors);
        return (TError)(ResultError)compositeError;
    }

    #endregion
}
