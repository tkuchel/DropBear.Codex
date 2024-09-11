#region

using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Extensions;

/// <summary>
///     Provides extension methods for working with <see cref="Result" /> types.
/// </summary>
public static class ResultExtensions
{
#pragma warning disable CS0081, CS1584, CS1658
    /// <summary>
    ///     Converts a reference type value to a <see cref="Result{T}" />.
    ///     If the value is null, returns a failure result with an appropriate error message.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="Result{T}" /> representing success if the value is not null, otherwise a failure.</returns>
    public static Result<T> ToResult<T>(this T? value) where T : class
    {
        return value is not null ? Result<T>.Success(value) : Result<T>.Failure("Value is null");
    }

    /// <summary>
    ///     Converts a nullable value type to a <see cref="Result{T}" />.
    ///     If the value is null, returns a failure result with an appropriate error message.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The nullable value to convert.</param>
    /// <returns>A <see cref="Result{T}" /> representing success if the value is not null, otherwise a failure.</returns>
    public static Result<T> ToResult<T>(this T? value) where T : struct
    {
        return value.HasValue ? Result<T>.Success(value.Value) : Result<T>.Failure("Value is null");
    }

    /// <summary>
    ///     Aggregates a collection of <see cref="Result{T}" /> into a single <see cref="Result{T}" />.
    ///     If any result in the collection is a failure, returns a failure with a combined error message.
    /// </summary>
    /// <typeparam name="T">The type of the value contained in each <see cref="Result{T}" />.</typeparam>
    /// <param name="results">The collection of <see cref="Result{T}" /> to aggregate.</param>
    /// <returns>
    ///     A <see cref="Result{IEnumerable{T}}" /> representing success if all results are successful, otherwise a
    ///     failure.
    /// </returns>
    public static Result<IEnumerable<T>> Traverse<T>(this IEnumerable<Result<T>> results)
    {
        var successes = new List<T>();
        var errors = new List<string>();

        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                successes.Add(result.Value);
            }
            else
            {
                errors.Add(result.ErrorMessage);
            }
        }

        return errors.Count == 0
            ? Result<IEnumerable<T>>.Success(successes)
            : Result<IEnumerable<T>>.Failure(string.Join(", ", errors));
    }

    /// <summary>
    ///     Alias for <see cref="Traverse{T}(IEnumerable{Result{T}})" />.
    ///     Aggregates a collection of <see cref="Result{T}" /> into a single <see cref="Result{T}" />.
    /// </summary>
    /// <typeparam name="T">The type of the value contained in each <see cref="Result{T}" />.</typeparam>
    /// <param name="results">The collection of <see cref="Result{T}" /> to aggregate.</param>
    /// <returns>
    ///     A <see cref="Result{IEnumerable{T}}" /> representing success if all results are successful, otherwise a
    ///     failure.
    /// </returns>
    public static Result<IEnumerable<T>> Sequence<T>(this IEnumerable<Result<T>> results)
    {
        return results.Traverse();
    }

    /// <summary>
    ///     Executes an asynchronous action if the <see cref="Result" /> is successful.
    /// </summary>
    /// <param name="result">The <see cref="Result" /> to check.</param>
    /// <param name="action">The asynchronous action to execute if the result is successful.</param>
    /// <returns>The original <see cref="Result" />.</returns>
    public static async Task<Result> OnSuccessAsync(this Result result, Func<Task> action)
    {
        if (result.IsSuccess)
        {
            await action().ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    ///     Executes an asynchronous action if the <see cref="Result" /> is a failure.
    /// </summary>
    /// <param name="result">The <see cref="Result" /> to check.</param>
    /// <param name="action">The asynchronous action to execute if the result is a failure.</param>
    /// <returns>The original <see cref="Result" />.</returns>
    public static async Task<Result> OnFailureAsync(this Result result, Func<string, Exception?, Task> action)
    {
        if (result.IsFailure())
        {
            await action(result.ErrorMessage, result.Exception).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    ///     Maps the value of a successful <see cref="Result{T}" /> asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the value in the original result.</typeparam>
    /// <typeparam name="TOut">The type of the value in the resulting result.</typeparam>
    /// <param name="result">The original <see cref="Result{T}" /> to map.</param>
    /// <param name="mapper">The asynchronous function to apply to the value of the result.</param>
    /// <returns>
    ///     A <see cref="Result{TOut}" /> representing success with the mapped value, or a failure if the original result
    ///     was not successful.
    /// </returns>
    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Result<T> result, Func<T, Task<TOut>> mapper)
    {
        return result.IsSuccess
            ? Result<TOut>.Success(await mapper(result.Value).ConfigureAwait(false))
            : Result<TOut>.Failure(result.ErrorMessage, result.Exception);
    }

    /// <summary>
    ///     Combines two <see cref="Result{T}" /> objects into a <see cref="Result{T}" /> containing a tuple of the two values.
    ///     If either result is a failure, returns a failure with a combined error message.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <param name="result1">The first result to combine.</param>
    /// <param name="result2">The second result to combine.</param>
    /// <returns>
    ///     A <see cref="Result{(T1, T2)}" /> containing the two values if both results are successful, otherwise a
    ///     failure.
    /// </returns>
    public static Result<(T1, T2)> Combine<T1, T2>(this Result<T1> result1, Result<T2> result2)
    {
        if (result1.IsSuccess && result2.IsSuccess)
        {
            return Result<(T1, T2)>.Success((result1.Value, result2.Value));
        }

        var errorMessage =
            $"{result1.ErrorMessage}{("; ")}{result2.ErrorMessage}";
        return Result<(T1, T2)>.Failure(errorMessage);
    }

    /// <summary>
    ///     Determines whether the <see cref="Result" /> is a failure.
    /// </summary>
    /// <param name="result">The result to check.</param>
    /// <returns>True if the result is a failure, otherwise false.</returns>
    public static bool IsFailure(this Result result)
    {
        return result.State == ResultState.Failure;
    }

#pragma warning restore CS0081, CS1584, CS1658
}
