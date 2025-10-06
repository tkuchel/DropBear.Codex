#region

using System.Diagnostics;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;

#endregion

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Base interface for all result types in the library, providing a common
///     set of properties to indicate success/failure state and associated exceptions.
/// </summary>
public interface IResult
{
    /// <summary>
    ///     Gets a value indicating whether the result is in a success state.
    ///     (Typically determined by <see cref="ResultState" />.)
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    ///     Gets the <see cref="ResultState" /> indicating overall success/failure or other states.
    /// </summary>
    ResultState State { get; }

    /// <summary>
    ///     If a failure has occurred, this is the main exception, if any.
    ///     (May be <c>null</c> if the error did not arise from a thrown exception.)
    /// </summary>
    Exception? Exception { get; }

    /// <summary>
    ///     Gets any additional exceptions aggregated by the operation.
    ///     Usually this is either zero or one exception, but in
    ///     some operations multiple exceptions may be collected.
    /// </summary>
    IReadOnlyCollection<Exception> Exceptions { get; }
}

/// <summary>
///     Interface representing a result that can hold a specific error type <typeparamref name="TError" />,
///     extending the basic <see cref="IResult" /> interface.
/// </summary>
/// <typeparam name="TError">A type that inherits from <see cref="ResultError" />.</typeparam>
public interface IResult<out TError> : IResult
    where TError : ResultError
{
    /// <summary>
    ///     Gets the error object (if any) that describes what went wrong
    ///     when <see cref="IResult.IsSuccess" /> is false.
    ///     This property is covariant, meaning it can be cast to a more derived type.
    /// </summary>
    TError? Error { get; }
}

/// <summary>
///     A read-only interface representing a result that has a value of type <typeparamref name="T" />
///     and an error of type <typeparamref name="TError" /> (derived from <see cref="ResultError" />).
///     This interface is typically used for results where the value can be inspected but not modified.
/// </summary>
/// <typeparam name="T">The type of the value that the result holds (if successful).</typeparam>
/// <typeparam name="TError">A type deriving from <see cref="ResultError" /> representing the error.</typeparam>
public interface IReadOnlyResult<out T, out TError> : IResult<TError>
    where TError : ResultError
{
    /// <summary>
    ///     Gets the value of the result, or <c>null</c> if the result is not successful
    ///     or if the value itself is <c>null</c>.
    /// </summary>
    T? Value { get; }
}

/// <summary>
///     A result type that contains either a value of type <typeparamref name="T" />
///     or an error of type <typeparamref name="TError" />.
///     Provides common operations to manipulate or retrieve the value safely.
/// </summary>
/// <typeparam name="T">The type of the result value if successful.</typeparam>
/// <typeparam name="TError">A type deriving from <see cref="ResultError" /> representing the error.</typeparam>
public interface IResult<T, TError> : IReadOnlyResult<T, TError>
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

/// <summary>
///     Provides diagnostic capabilities for result operations.
/// </summary>
public interface IResultDiagnostics
{
    /// <summary>
    ///     Gets diagnostic information about a result.
    /// </summary>
    DiagnosticInfo GetDiagnostics();

    /// <summary>
    ///     Gets the trace context for a result operation.
    /// </summary>
    ActivityContext GetTraceContext();
}

/// <summary>
///     Provides telemetry information for result operations.
/// </summary>
public interface IResultTelemetry
{
    /// <summary>
    ///     Tracks when a result is created.
    /// </summary>
    void TrackResultCreated(ResultState state, Type resultType, string? caller = null);

    /// <summary>
    ///     Tracks when a result is transformed from one state to another.
    /// </summary>
    void TrackResultTransformed(ResultState originalState, ResultState newState, Type resultType,
        string? caller = null);

    /// <summary>
    ///     Tracks when an exception occurs during a result operation.
    /// </summary>
    void TrackException(Exception exception, ResultState state, Type resultType,
        string? caller = null);
}

/// <summary>
///     Provides custom error handling capabilities for results.
/// </summary>
public interface IResultErrorHandler
{
    /// <summary>
    ///     Handles exceptions by converting them to result failures.
    /// </summary>
    Result<T, TError> HandleError<T, TError>(Exception exception)
        where TError : ResultError;

    /// <summary>
    ///     Handles errors by creating appropriate result failures.
    /// </summary>
    Result<T, TError> HandleError<T, TError>(TError error)
        where TError : ResultError;
}

/// <summary>
///     Provides a custom async enumerable pattern for results.
/// </summary>
public interface IAsyncEnumerableResult<out T> : IAsyncEnumerable<T>
{
    /// <summary>
    ///     Gets the <see cref="ResultState" /> indicating the success or failure status of this result.
    /// </summary>
    ResultState State { get; }

    /// <summary>
    ///     Gets a value indicating whether this result represents a successful operation.
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    ///     Gets an optional exception if the result represents a failure that threw an exception.
    /// </summary>
    Exception? Exception { get; }

    /// <summary>
    ///     Gets a read-only collection of exceptions if multiple exceptions occurred.
    /// </summary>
    IReadOnlyCollection<Exception> Exceptions { get; }

    /// <summary>
    ///     Gets the count of items in the enumerable.
    /// </summary>
    ValueTask<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if the enumerable has any items.
    /// </summary>
    ValueTask<bool> HasItemsAsync(CancellationToken cancellationToken = default);
}
