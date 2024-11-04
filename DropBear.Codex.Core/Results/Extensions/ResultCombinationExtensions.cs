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
    public static Result<(T1, T2), TError> Combine<T1, T2, TError>(
        this Result<T1, TError> result1,
        Result<T2, TError> result2)
        where TError : ResultError
    {
        if (result1.IsSuccess && result2.IsSuccess)
        {
            return Result<(T1, T2), TError>.Success((result1.Value, result2.Value));
        }

        var errors = new List<TError>();
        if (!result1.IsSuccess && result1.Error != null)
        {
            errors.Add(result1.Error);
        }

        if (!result2.IsSuccess && result2.Error != null)
        {
            errors.Add(result2.Error);
        }

        return Result<(T1, T2), TError>.Failure(
            (TError)(ResultError)new CompositeError(errors));
    }

    public static Result<(T1, T2, T3), TError> Combine<T1, T2, T3, TError>(
        this Result<T1, TError> result1,
        Result<T2, TError> result2,
        Result<T3, TError> result3)
        where TError : ResultError
    {
        if (result1.IsSuccess && result2.IsSuccess && result3.IsSuccess)
        {
            return Result<(T1, T2, T3), TError>.Success((result1.Value, result2.Value, result3.Value));
        }

        var errors = new List<TError>();
        if (!result1.IsSuccess && result1.Error != null)
        {
            errors.Add(result1.Error);
        }

        if (!result2.IsSuccess && result2.Error != null)
        {
            errors.Add(result2.Error);
        }

        if (!result3.IsSuccess && result3.Error != null)
        {
            errors.Add(result3.Error);
        }

        return Result<(T1, T2, T3), TError>.Failure(
            (TError)(ResultError)new CompositeError(errors));
    }

    public static Result<IEnumerable<T>, TError> Sequence<T, TError>(
        this IEnumerable<Result<T, TError>> results)
        where TError : ResultError
    {
        return results.Traverse();
    }

    public static Result<IEnumerable<T>, TError> Traverse<T, TError>(
        this IEnumerable<Result<T, TError>> results)
        where TError : ResultError
    {
        var successes = new List<T>();
        var errors = new List<TError>();

        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                successes.Add(result.Value);
            }
            else if (result.Error != null)
            {
                errors.Add(result.Error);
            }
        }

        if (errors.Count == 0)
        {
            return Result<IEnumerable<T>, TError>.Success(successes);
        }

        return successes.Any()
            ? Result<IEnumerable<T>, TError>.PartialSuccess(
                successes,
                (TError)(ResultError)new CompositeError(errors))
            : Result<IEnumerable<T>, TError>.Failure(
                (TError)(ResultError)new CompositeError(errors));
    }

    public static Result<T, TError> First<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<TError> onEmpty)
        where TError : ResultError
    {
        return result.Bind(xs => xs.Any()
            ? Result<T, TError>.Success(xs.First())
            : Result<T, TError>.Failure(onEmpty()));
    }

    public static Result<T, TError> FirstOrDefault<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        T defaultValue)
        where TError : ResultError
    {
        return result.Map(xs => xs.FirstOrDefault(defaultValue));
    }
}
