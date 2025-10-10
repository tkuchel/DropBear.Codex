using DropBear.Codex.Core.Results.Base;

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     A result type that contains either a value of type <typeparamref name="T" />
///     or an error of type <typeparamref name="TError" />.
///     Provides common operations to manipulate or retrieve the value safely.
/// </summary>
/// <typeparam name="T">The type of the result value if successful.</typeparam>
/// <typeparam name="TError">A type deriving from <see cref="ResultError" /> representing the error.</typeparam>
#pragma warning disable MA0048
public interface IResult<T, TError> : IReadOnlyResult<T, TError>
#pragma warning restore MA0048
    where TError : ResultError
{
    /// <summary>
    ///     Ensures that the given <paramref name="predicate" /> is satisfied by the
    ///     <see cref="IReadOnlyResult{T, TError}.Value" />.
    ///     If the predicate fails, sets <paramref name="error" /> as the error for this result.
    /// </summary>
    /// <param name="predicate">A function that returns <c>true</c> if the value is acceptable.</param>
    /// <param name="error">The error to set if the predicate is not satisfied.</param>
    /// <returns>This result (for fluent chaining), possibly transformed into a failure state.</returns>
    IResult<T, TError> Ensure(Func<T, bool> predicate, TError error);

    /// <summary>
    ///     Returns the <see cref="IReadOnlyResult{T, TError}.Value" /> if available, otherwise returns
    ///     <paramref name="defaultValue" />.
    /// </summary>
    /// <param name="defaultValue">A default fallback if the result is not successful.</param>
    /// <returns>The result value if successful, otherwise <paramref name="defaultValue" />.</returns>
    T ValueOrDefault(T defaultValue = default!);

    /// <summary>
    ///     Returns the <see cref="IReadOnlyResult{T, TError}.Value" /> if available, otherwise throws an exception.
    ///     Optionally, you can specify an <paramref name="errorMessage" /> for the thrown exception.
    /// </summary>
    /// <param name="errorMessage">An optional message for the exception if the result is not successful.</param>
    /// <returns>The result value if successful.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the result is not successful.</exception>
    T ValueOrThrow(string? errorMessage = null);
}
