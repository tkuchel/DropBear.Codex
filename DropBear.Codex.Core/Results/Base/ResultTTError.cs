#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Errors;
using DropBear.Codex.Core.Results.Extensions;
using DropBear.Codex.Core.Results.Serialization;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     A concrete result type that can contain both a value and an error.
///     Optimized for .NET 9 with direct allocation and modern value handling.
/// </summary>
/// <typeparam name="T">The type of the successful value.</typeparam>
/// <typeparam name="TError">A type inheriting from <see cref="ResultError" />.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonConverter(typeof(ResultTTErrorJsonConverter<,>))]
public class Result<T, TError> : Result<TError>, IResult<T, TError>
    where TError : ResultError
{
    // Use modern struct container for better performance
    private readonly ValueContainer _valueContainer;

    /// <summary>
    ///     Initializes a new instance of Result{T, TError}.
    ///     Optimized for .NET 9 with struct-based value storage.
    /// </summary>
    protected Result(T? value, ResultState state, TError? error = null, Exception? exception = null)
        : base(state, error, exception)
    {
        _valueContainer = new ValueContainer(value, state.IsSuccessState());
    }

    #region Debugger Display

    // Use 'new' keyword to hide the base property
    private new string DebuggerDisplay =>
        $"State = {State}, Success = {IsSuccess}, " +
        $"Value = {(_valueContainer.HasValue ? _valueContainer.Value?.ToString() : "null")}, " +
        $"Error = {Error?.Message ?? "null"}";

    #endregion

    #region Pooling Support for Compatibility Layer

    /// <summary>
    ///     Initializes the result instance with a value and state.
    ///     Internal method used by the pooling compatibility layer.
    /// </summary>
    /// <param name="value">The result value.</param>
    /// <param name="state">The result state.</param>
    /// <param name="error">The error object.</param>
    /// <param name="exception">Optional exception.</param>
    protected internal void InitializeFromValue(T? value, ResultState state, TError? error, Exception? exception)
    {
        ValidateErrorState(state, error);

        // Update the base state
        SetStateInternal(state, exception);

        // Update the error
        Error = error;

        // Update the value container using Unsafe.AsRef to modify the readonly field
        // This is safe because we're in a controlled pooling scenario
        Unsafe.AsRef(in _valueContainer) = new ValueContainer(value, state.IsSuccessState());
    }

    #endregion

    #region Nested Types

    /// <summary>
    ///     Modern value container using readonly struct for optimal performance.
    ///     Optimized for .NET 9 with minimal allocations.
    ///     Nested as a private type to avoid visibility issues.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ValueContainer(T? Value, bool HasValue);

    #endregion

    #region IResult<T, TError> Implementation

    /// <inheritdoc />
    public T? Value => _valueContainer.Value;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ValueOrDefault(T defaultValue = default!) =>
        _valueContainer.HasValue ? _valueContainer.Value! : defaultValue;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ValueOrThrow(string? errorMessage = null)
    {
        if (_valueContainer.HasValue)
        {
            return _valueContainer.Value!;
        }

        var message = errorMessage ?? Error?.Message ?? "Operation failed";
        throw new ResultException(message);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IResult<T, TError> Ensure(Func<T, bool> predicate, TError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (!IsSuccess || !_valueContainer.HasValue)
        {
            return this;
        }

        try
        {
            return predicate(_valueContainer.Value!)
                ? this
                : Failure(error);
        }
        catch (Exception ex)
        {
            return Failure(error, ex);
        }
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a new Result in the Success state with a value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Success(T value) => new(value, ResultState.Success);

    /// <summary>
    ///     Creates a new Result in the Failure state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static new Result<T, TError> Failure(TError error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(default, ResultState.Failure, error, exception);
    }

    /// <summary>
    ///     Creates a new Result in the Warning state without a value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static new Result<T, TError> Warning(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(default, ResultState.Warning, error);
    }

    /// <summary>
    ///     Creates a new Result in the Warning state with a value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Warning(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(value, ResultState.Warning, error);
    }

    /// <summary>
    ///     Creates a new Result in the PartialSuccess state with a value and error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> PartialSuccess(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(value, ResultState.PartialSuccess, error);
    }

    /// <summary>
    ///     Creates a new Result in the Cancelled state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static new Result<T, TError> Cancelled(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(default, ResultState.Cancelled, error);
    }

    /// <summary>
    ///     Creates a new Result in the Cancelled state with a value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Cancelled(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(value, ResultState.Cancelled, error);
    }

    /// <summary>
    ///     Creates a new Result in the Pending state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static new Result<T, TError> Pending(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(default, ResultState.Pending, error);
    }

    /// <summary>
    ///     Creates a new Result in the Pending state with a value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Pending(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(value, ResultState.Pending, error);
    }

    /// <summary>
    ///     Creates a new Result in the NoOp state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static new Result<T, TError> NoOp(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(default, ResultState.NoOp, error);
    }

    /// <summary>
    ///     Creates a new Result in the NoOp state with a value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> NoOp(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(value, ResultState.NoOp, error);
    }

    #endregion

    #region Operators

    /// <summary>
    ///     Implicit conversion from value to success result.
    /// </summary>
    public static implicit operator Result<T, TError>(T value) => Success(value);

    /// <summary>
    ///     Implicit conversion from error to failure result.
    /// </summary>
    public static implicit operator Result<T, TError>(TError error) => Failure(error);

    #endregion
}
