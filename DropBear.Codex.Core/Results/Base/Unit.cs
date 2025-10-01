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
///     Optimized for .NET 9 with improved performance and reduced allocations.
/// </summary>
[DebuggerDisplay("Unit")]
[JsonConverter(typeof(UnitJsonConverter))]
public readonly record struct Unit : ISpanFormattable
{
    /// <summary>
    ///     Gets the single <see cref="Unit" /> value.
    /// </summary>
    public static Unit Value { get; } = default;

    #region Task and ValueTask Conversions

    /// <summary>
    ///     Gets a completed ValueTask of Unit for immediate return.
    /// </summary>
    public static ValueTask<Unit> CompletedTask => ValueTask.FromResult(Value);

    /// <summary>
    ///     Creates a canceled ValueTask of Unit with the specified cancellation token.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Unit> FromCanceled(CancellationToken cancellationToken)
    {
        return ValueTask.FromCanceled<Unit>(cancellationToken);
    }

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
    ///     Converts a void-returning asynchronous function to a Unit-returning one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> FromAsync(Func<ValueTask> asyncAction)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);
        await asyncAction().ConfigureAwait(false);
        return Value;
    }

    /// <summary>
    ///     Converts a void-returning Task function to a Unit-returning ValueTask.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> FromAsync(Func<Task> asyncAction)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);
        await asyncAction().ConfigureAwait(false);
        return Value;
    }

    /// <summary>
    ///     Converts a void-returning async function with parameter to a Unit-returning ValueTask.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Func<T, ValueTask<Unit>> FromAsync<T>(Func<T, ValueTask> asyncAction)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);
        return async parameter =>
        {
            await asyncAction(parameter).ConfigureAwait(false);
            return Value;
        };
    }

    #endregion

    #region Task Continuations

    /// <summary>
    ///     Executes the specified action after the ValueTask completes and returns Unit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ContinueWith(ValueTask task, Action continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        await task.ConfigureAwait(false);
        continuation();
        return Value;
    }

    /// <summary>
    ///     Executes the specified action after the Task completes and returns Unit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ContinueWith(Task task, Action continuation)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(continuation);
        await task.ConfigureAwait(false);
        continuation();
        return Value;
    }

    /// <summary>
    ///     Executes the specified action with the ValueTask result after the task completes and returns Unit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ContinueWith<T>(ValueTask<T> task, Action<T> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        var result = await task.ConfigureAwait(false);
        continuation(result);
        return Value;
    }

    /// <summary>
    ///     Executes the specified action with the Task result after the task completes and returns Unit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ContinueWith<T>(Task<T> task, Action<T> continuation)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(continuation);
        var result = await task.ConfigureAwait(false);
        continuation(result);
        return Value;
    }

    /// <summary>
    ///     Executes the specified async action after the ValueTask completes and returns Unit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ContinueWithAsync(ValueTask task, Func<ValueTask> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        await task.ConfigureAwait(false);
        await continuation().ConfigureAwait(false);
        return Value;
    }

    /// <summary>
    ///     Executes the specified async action with the ValueTask result after the task completes and returns Unit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ContinueWithAsync<T>(ValueTask<T> task, Func<T, ValueTask> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        var result = await task.ConfigureAwait(false);
        await continuation(result).ConfigureAwait(false);
        return Value;
    }

    #endregion

    #region Formatting

    private const string UnitString = "()";
    private static ReadOnlySpan<char> UnitSpan => "()";

    /// <summary>
    ///     Returns a string representation of the Unit value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => UnitString;

    /// <summary>
    ///     Returns a string representation of the Unit value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(string? format, IFormatProvider? formatProvider) => UnitString;

    /// <summary>
    ///     Attempts to format the Unit value into the provided character span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        if (destination.Length < UnitSpan.Length)
        {
            charsWritten = 0;
            return false;
        }

        UnitSpan.CopyTo(destination);
        charsWritten = UnitSpan.Length;
        return true;
    }

    #endregion

    #region Implicit Conversions

    /// <summary>
    ///     Implicitly converts from <see cref="Unit" /> to a completed <see cref="ValueTask" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ValueTask(Unit _) => ValueTask.CompletedTask;

    /// <summary>
    ///     Implicitly converts from <see cref="Unit" /> to a completed <see cref="Task" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Task(Unit _) => Task.CompletedTask;

    /// <summary>
    ///     Implicitly converts from <see cref="Unit" /> to a completed <see cref="ValueTask{Unit}" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ValueTask<Unit>(Unit _) => CompletedTask;

    #endregion

    #region Factory Methods
    /// <summary>
    ///     Creates a Unit result after executing a synchronous action.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Unit Execute(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
        return Value;
    }

    /// <summary>
    ///     Creates a Unit result after executing an asynchronous action.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ExecuteAsync(Func<ValueTask> asyncAction)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);
        await asyncAction().ConfigureAwait(false);
        return Value;
    }

    /// <summary>
    ///     Creates a Unit result after executing an asynchronous Task-based action.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ExecuteAsync(Func<Task> asyncAction)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);
        await asyncAction().ConfigureAwait(false);
        return Value;
    }

    #endregion
}
