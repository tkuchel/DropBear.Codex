#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Results.Serialization;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Represents a type that has a single possible value, commonly used in functional programming
///     to indicate "no meaningful return value" without using <see langword="void" />.
///     Optimized for .NET 9 with improved performance and reduced allocations.
/// </summary>
[DebuggerDisplay("Unit")]
[JsonConverter(typeof(UnitJsonConverter))]
public readonly record struct Unit : ISpanFormattable
{
    /// <summary>
    ///     Gets the single <see cref="Unit" /> value.
    /// </summary>
    public static Unit Value { get; }

    #region Task and ValueTask Conversions

    /// <summary>
    ///     Gets a completed ValueTask of Unit for immediate return.
    /// </summary>
    public static ValueTask<Unit> CompletedTask => ValueTask.FromResult(Value);

    /// <summary>
    ///     Creates a canceled ValueTask of Unit with the specified cancellation token.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Unit> FromCanceled(CancellationToken cancellationToken) =>
        ValueTask.FromCanceled<Unit>(cancellationToken);

    /// <summary>
    ///     Creates a ValueTask of Unit that has completed with the specified exception.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Unit> FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return ValueTask.FromException<Unit>(exception);
    }

    #endregion

    #region Value Conversions

    /// <summary>
    ///     Converts a void-returning Action to a Unit-returning Function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Func<Unit> FromAction(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return () =>
        {
            action();
            return Value;
        };
    }

    /// <summary>
    ///     Converts a void-returning Action with parameter to a Unit-returning Function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Func<T, Unit> FromAction<T>(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return parameter =>
        {
            action(parameter);
            return Value;
        };
    }

    /// <summary>
    ///     Converts a void-returning Action with two parameters to a Unit-returning Function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Func<T1, T2, Unit> FromAction<T1, T2>(Action<T1, T2> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return (param1, param2) =>
        {
            action(param1, param2);
            return Value;
        };
    }

    /// <summary>
    ///     Converts a void-returning asynchronous function to a Unit-returning one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Func<ValueTask<Unit>> FromActionAsync(Func<ValueTask> actionAsync)
    {
        ArgumentNullException.ThrowIfNull(actionAsync);
        return async () =>
        {
            await actionAsync().ConfigureAwait(false);
            return Value;
        };
    }

    /// <summary>
    ///     Converts a void-returning asynchronous function with parameter to a Unit-returning one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Func<T, ValueTask<Unit>> FromActionAsync<T>(Func<T, ValueTask> actionAsync)
    {
        ArgumentNullException.ThrowIfNull(actionAsync);
        return async parameter =>
        {
            await actionAsync(parameter).ConfigureAwait(false);
            return Value;
        };
    }

    #endregion

    #region ISpanFormattable Implementation

    /// <summary>
    ///     Formats the value of the current instance using the specified format.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider) => "()";

    /// <summary>
    ///     Tries to format the value of the current instance into the provided span of characters.
    /// </summary>
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        if (destination.Length >= 2)
        {
            destination[0] = '(';
            destination[1] = ')';
            charsWritten = 2;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    #endregion

    #region Equality and Comparison

    /// <summary>
    ///     All Unit values are equal.
    /// </summary>
    public override string ToString() => "()";

    /// <summary>
    ///     Gets the hash code (always the same for Unit).
    /// </summary>
    public override int GetHashCode() => 0;

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Discards a value and returns Unit.
    ///     Use this to explicitly ignore a value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Unit Discard<T>(T _) => Value;

    /// <summary>
    ///     Executes an action and returns Unit.
    ///     Useful for converting void-returning methods to expressions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Unit Do(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
        return Value;
    }

    /// <summary>
    ///     Executes an action with a parameter and returns Unit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Unit Do<T>(T value, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action(value);
        return Value;
    }

    #endregion
}
