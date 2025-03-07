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
[DebuggerDisplay("Unit")]
[JsonConverter(typeof(UnitJsonConverter))]
public readonly struct Unit : IEquatable<Unit>, ISpanFormattable
{
    /// <summary>
    ///     Gets the single <see cref="Unit" /> value.
    /// </summary>
    public static Unit Value { get; } = default;

    #region Equality Implementation

    /// <summary>
    ///     Always returns true since all Unit values are equal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Unit other)
    {
        return true;
    }

    /// <summary>
    ///     Returns true if the object is a Unit.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is Unit;
    }

    /// <summary>
    ///     Returns a constant hash code for all Unit values.
    /// </summary>
    public override int GetHashCode()
    {
        return 0;
    }

    public static bool operator ==(Unit left, Unit right)
    {
        return true;
    }

    public static bool operator !=(Unit left, Unit right)
    {
        return false;
    }

    #endregion

    #region Formatting

    public override string ToString()
    {
        return "()";
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return "()";
    }

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

    public static implicit operator ValueTask(Unit _)
    {
        return default;
    }

    public static implicit operator Task(Unit _)
    {
        return Task.CompletedTask;
    }

    #endregion
}
