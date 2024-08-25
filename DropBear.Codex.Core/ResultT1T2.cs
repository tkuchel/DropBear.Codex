#region

using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core;

/// <summary>
///     Represents the outcome of an operation that can result in either a success of type <typeparamref name="TSuccess" />
///     or a failure of type <typeparamref name="TFailure" />.
/// </summary>
/// <typeparam name="TSuccess">The type of the success value.</typeparam>
/// <typeparam name="TFailure">The type of the failure value.</typeparam>
#pragma warning disable MA0048
public class Result<TSuccess, TFailure> : IEquatable<Result<TSuccess, TFailure>>
#pragma warning restore MA0048
{
    private readonly TFailure _failure;
    private readonly TSuccess _success;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result{TSuccess, TFailure}" /> class.
    /// </summary>
    /// <param name="success">The success value.</param>
    /// <param name="failure">The failure value.</param>
    /// <param name="state">The state of the result.</param>
    private Result(TSuccess success, TFailure failure, ResultState state)
    {
        _success = success;
        _failure = failure;
        State = state;
    }

    /// <summary>
    ///     Gets the state of the result.
    /// </summary>
    public ResultState State { get; }

    /// <summary>
    ///     Gets a value indicating whether the result is a success.
    /// </summary>
    public bool IsSuccess => State == ResultState.Success;

    /// <summary>
    ///     Gets the success value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing the success value on a failed result.</exception>
    public TSuccess Success =>
        IsSuccess ? _success : throw new InvalidOperationException("Cannot access Success on a failed result.");

    /// <summary>
    ///     Gets the failure value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing the failure value on a successful result.</exception>
    public TFailure Failure =>
        !IsSuccess ? _failure : throw new InvalidOperationException("Cannot access Failure on a successful result.");

    /// <summary>
    ///     Determines whether the specified result is equal to the current result.
    /// </summary>
    /// <param name="other">The result to compare with the current result.</param>
    /// <returns>True if the specified result is equal to the current result; otherwise, false.</returns>
    public bool Equals(Result<TSuccess, TFailure>? other)
    {
        return other is not null && State == other.State &&
               EqualityComparer<TSuccess>.Default.Equals(_success, other._success) &&
               EqualityComparer<TFailure>.Default.Equals(_failure, other._failure);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as Result<TSuccess, TFailure>);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(State, _success, _failure);
    }

    /// <summary>
    ///     Creates a successful result with the specified success value.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <returns>A new <see cref="Result{TSuccess, TFailure}" /> representing a successful result.</returns>
    public static Result<TSuccess, TFailure> Succeeded(TSuccess value)
    {
        return new Result<TSuccess, TFailure>(value, default!, ResultState.Success);
    }

    /// <summary>
    ///     Creates a failed result with the specified failure value.
    /// </summary>
    /// <param name="error">The failure value.</param>
    /// <returns>A new <see cref="Result{TSuccess, TFailure}" /> representing a failed result.</returns>
    public static Result<TSuccess, TFailure> Failed(TFailure error)
    {
        return new Result<TSuccess, TFailure>(default!, error, ResultState.Failure);
    }

    /// <summary>
    ///     Matches the current result to a corresponding function based on its state.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the functions.</typeparam>
    /// <param name="onSuccess">The function to execute if the result is a success.</param>
    /// <param name="onFailure">The function to execute if the result is a failure.</param>
    /// <returns>The result of the corresponding function based on the state.</returns>
    public TResult Match<TResult>(
        Func<TSuccess, TResult> onSuccess,
        Func<TFailure, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(_success) : onFailure(_failure);
    }

    /// <summary>
    ///     Matches the current result to a corresponding action based on its state.
    /// </summary>
    /// <param name="onSuccess">The action to execute if the result is a success.</param>
    /// <param name="onFailure">The action to execute if the result is a failure.</param>
    public void Match(
        Action<TSuccess> onSuccess,
        Action<TFailure> onFailure)
    {
        if (IsSuccess)
        {
            onSuccess(_success);
        }
        else
        {
            onFailure(_failure);
        }
    }

    /// <summary>
    ///     Applies a function to the success value of the result if it is successful, returning a new result of type
    ///     <typeparamref name="TNewSuccess" />.
    /// </summary>
    /// <typeparam name="TNewSuccess">The type of the value returned by the function.</typeparam>
    /// <param name="onSuccess">The function to apply to the success value.</param>
    /// <returns>
    ///     A new result containing the value returned by the function, or a failure result if the original result was not
    ///     successful.
    /// </returns>
    public Result<TNewSuccess, TFailure> Bind<TNewSuccess>(
        Func<TSuccess, Result<TNewSuccess, TFailure>> onSuccess)
    {
        return IsSuccess ? onSuccess(_success) : Result<TNewSuccess, TFailure>.Failed(_failure);
    }

    /// <summary>
    ///     Maps the success value of the result to a new value using the specified mapper function.
    /// </summary>
    /// <typeparam name="TNewSuccess">The type of the value returned by the mapper function.</typeparam>
    /// <param name="mapper">The function to apply to the success value.</param>
    /// <returns>A new result containing the mapped value, or a failure result if the original result was not successful.</returns>
    public Result<TNewSuccess, TFailure> Map<TNewSuccess>(Func<TSuccess, TNewSuccess> mapper)
    {
        return IsSuccess
            ? Result<TNewSuccess, TFailure>.Succeeded(mapper(_success))
            : Result<TNewSuccess, TFailure>.Failed(_failure);
    }

    public static implicit operator Result<TSuccess, TFailure>(TSuccess success)
    {
        return Succeeded(success);
    }

    public static implicit operator Result<TSuccess, TFailure>(TFailure failure)
    {
        return Failed(failure);
    }
}
