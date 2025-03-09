#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Results.Compatibility;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Represents a type that has a single possible value, commonly used in functional programming
///     to indicate "no meaningful return value" without using <see langword="void" />.
///     This is useful for operations that succeed or fail but don't produce a value.
/// </summary>
/// <remarks>
///     <para>
///         Unit serves as a more type-safe alternative to void in functional programming.
///         It explicitly represents the concept of "no value" as an actual value, rather
///         than the absence of a value that void represents.
///     </para>
///     <para>
///         All Unit values are considered equal. This type provides utilities for converting
///         void-returning methods into Unit-returning methods, making them easier to use
///         with the Result pattern and functional composition.
///     </para>
/// </remarks>
[DebuggerDisplay("Unit")]
[JsonConverter(typeof(UnitJsonConverter))]
public readonly struct Unit : IEquatable<Unit>, ISpanFormattable
{
    /// <summary>
    ///     Gets the single <see cref="Unit" /> value.
    /// </summary>
    public static Unit Value { get; } = default;

    #region Value Conversions

    /// <summary>
    ///     Converts a void-returning Action to a Unit-returning Function.
    /// </summary>
    /// <param name="action">The action to convert.</param>
    /// <returns>A function that executes the action and returns Unit.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="action" /> is null.</exception>
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
    ///     Converts a void-returning asynchronous function to a Unit-returning one.
    /// </summary>
    /// <param name="asyncAction">The asynchronous action to convert.</param>
    /// <returns>A task that completes when the async action completes and returns Unit.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="asyncAction" /> is null.</exception>
    public static async Task<Unit> FromAsync(Func<Task> asyncAction)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);

        await asyncAction().ConfigureAwait(false);
        return Value;
    }

    /// <summary>
    ///     Converts a void-returning asynchronous ValueTask function to a Unit-returning one.
    /// </summary>
    /// <param name="asyncAction">The asynchronous value task action to convert.</param>
    /// <returns>A value task that completes when the async action completes and returns Unit.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="asyncAction" /> is null.</exception>
    public static async ValueTask<Unit> FromAsyncValue(Func<ValueTask> asyncAction)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);

        await asyncAction().ConfigureAwait(false);
        return Value;
    }

    /// <summary>
    ///     Creates a canceled ValueTask of Unit with the specified cancellation token.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that caused the cancellation.</param>
    /// <returns>A canceled ValueTask containing Unit.</returns>
    public static ValueTask<Unit> FromCanceled(CancellationToken cancellationToken)
    {
        return ValueTask.FromCanceled<Unit>(cancellationToken);
    }

    /// <summary>
    ///     Creates a ValueTask of Unit that has completed with the specified exception.
    /// </summary>
    /// <param name="exception">The exception with which to complete the task.</param>
    /// <returns>A faulted ValueTask containing Unit.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="exception" /> is null.</exception>
    public static ValueTask<Unit> FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return ValueTask.FromException<Unit>(exception);
    }

    #endregion

    #region Task Continuations

    /// <summary>
    ///     Executes the specified action after the task completes and returns Unit.
    /// </summary>
    /// <param name="task">The task to continue from.</param>
    /// <param name="continuation">The action to execute after the task completes.</param>
    /// <returns>A ValueTask that completes when the continuation completes and returns Unit.</returns>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public static async ValueTask<Unit> ContinueWith(Task task, Action continuation)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(continuation);

        await task.ConfigureAwait(false);
        continuation();
        return Value;
    }

    /// <summary>
    ///     Executes the specified action with the task result after the task completes and returns Unit.
    /// </summary>
    /// <typeparam name="T">The type of the task result.</typeparam>
    /// <param name="task">The task to continue from.</param>
    /// <param name="continuation">The action to execute with the task result.</param>
    /// <returns>A ValueTask that completes when the continuation completes and returns Unit.</returns>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public static async ValueTask<Unit> ContinueWith<T>(Task<T> task, Action<T> continuation)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(continuation);

        var result = await task.ConfigureAwait(false);
        continuation(result);
        return Value;
    }

    /// <summary>
    ///     Executes the specified async action after the task completes and returns Unit.
    /// </summary>
    /// <param name="task">The task to continue from.</param>
    /// <param name="continuation">The async action to execute after the task completes.</param>
    /// <returns>A ValueTask that completes when the async continuation completes and returns Unit.</returns>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public static async ValueTask<Unit> ContinueWithAsync(Task task, Func<Task> continuation)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(continuation);

        await task.ConfigureAwait(false);
        await continuation().ConfigureAwait(false);
        return Value;
    }

    /// <summary>
    ///     Executes the specified async action with the task result after the task completes and returns Unit.
    /// </summary>
    /// <typeparam name="T">The type of the task result.</typeparam>
    /// <param name="task">The task to continue from.</param>
    /// <param name="continuation">The async action to execute with the task result.</param>
    /// <returns>A ValueTask that completes when the async continuation completes and returns Unit.</returns>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public static async ValueTask<Unit> ContinueWithAsync<T>(Task<T> task, Func<T, Task> continuation)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(continuation);

        var result = await task.ConfigureAwait(false);
        await continuation(result).ConfigureAwait(false);
        return Value;
    }

    #endregion

    #region Equality Implementation

    /// <summary>
    ///     Always returns true since all Unit values are equal.
    /// </summary>
    /// <param name="other">The other Unit to compare with.</param>
    /// <returns>Always returns true.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Unit other)
    {
        return true;
    }

    /// <summary>
    ///     Returns true if the object is a Unit.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the object is a Unit.</returns>
    public override bool Equals(object? obj)
    {
        return obj is Unit;
    }

    /// <summary>
    ///     Returns a constant hash code for all Unit values.
    /// </summary>
    /// <returns>A constant integer value.</returns>
    public override int GetHashCode()
    {
        return 0;
    }

    /// <summary>
    ///     Equality operator for Unit. Always returns true.
    /// </summary>
    /// <param name="left">The left Unit value.</param>
    /// <param name="right">The right Unit value.</param>
    /// <returns>Always returns true.</returns>
    public static bool operator ==(Unit left, Unit right)
    {
        return true;
    }

    /// <summary>
    ///     Inequality operator for Unit. Always returns false.
    /// </summary>
    /// <param name="left">The left Unit value.</param>
    /// <param name="right">The right Unit value.</param>
    /// <returns>Always returns false.</returns>
    public static bool operator !=(Unit left, Unit right)
    {
        return false;
    }

    #endregion

    #region Formatting

    /// <summary>
    ///     Returns a string representation of the Unit value.
    /// </summary>
    /// <returns>Always returns "()".</returns>
    public override string ToString()
    {
        return "()";
    }

    /// <summary>
    ///     Returns a string representation of the Unit value.
    /// </summary>
    /// <param name="format">The format string (ignored for Unit).</param>
    /// <param name="formatProvider">The format provider (ignored for Unit).</param>
    /// <returns>Always returns "()".</returns>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return "()";
    }

    /// <summary>
    ///     Attempts to format the Unit value into the provided character span.
    /// </summary>
    /// <param name="destination">The destination span where the formatted value will be written.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <param name="format">The format string (ignored for Unit).</param>
    /// <param name="provider">The format provider (ignored for Unit).</param>
    /// <returns>True if the formatting was successful, false if the destination span was too small.</returns>
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        if (destination.Length < 2)
        {
            charsWritten = 0;
            return false;
        }

        destination[0] = '(';
        destination[1] = ')';
        charsWritten = 2;
        return true;
    }

    #endregion

    #region Task Conversions

    /// <summary>
    ///     Implicitly converts from <see cref="Unit" /> to a completed <see cref="ValueTask" />.
    ///     Useful for returning from async methods where no value is needed but a task is required.
    /// </summary>
    /// <param name="_">The Unit value to convert.</param>
    public static implicit operator ValueTask(Unit _)
    {
        return default;
    }

    /// <summary>
    ///     Implicitly converts from <see cref="Unit" /> to a completed <see cref="Task" />.
    ///     Useful for returning from async methods where no value is needed but a task is required.
    /// </summary>
    /// <param name="_">The Unit value to convert.</param>
    public static implicit operator Task(Unit _)
    {
        return Task.CompletedTask;
    }

    #endregion
}
