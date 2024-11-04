#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides LINQ extension methods for Result types
/// </summary>
public static class ResultLinqExtensions
{
    public static Result<TResult, TError> Select<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, TResult> selector)
        where TError : ResultError
    {
        return result.Map(selector);
    }

    public static Result<TResult, TError> SelectMany<T, TIntermediate, TResult, TError>(
        this Result<T, TError> result,
        Func<T, Result<TIntermediate, TError>> intermediateSelector,
        Func<T, TIntermediate, TResult> resultSelector)
        where TError : ResultError
    {
        return result.Bind(x =>
            intermediateSelector(x).Map(y =>
                resultSelector(x, y)));
    }

    public static Result<T, TError> Where<T, TError>(
        this Result<T, TError> result,
        Func<T, bool> predicate,
        Func<T, TError> errorSelector)
        where TError : ResultError
    {
        return result.Bind(x => predicate(x)
            ? result
            : Result<T, TError>.Failure(errorSelector(x)));
    }

    public static Result<IEnumerable<TResult>, TError> Select<T, TResult, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, TResult> selector)
        where TError : ResultError
    {
        return result.Map(xs => xs.Select(selector));
    }

    public static Result<IEnumerable<T>, TError> Where<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, bool> predicate)
        where TError : ResultError
    {
        return result.Map(xs => xs.Where(predicate));
    }
}
