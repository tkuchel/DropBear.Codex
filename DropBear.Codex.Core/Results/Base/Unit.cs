#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Represents a type that has a single possible value, commonly used in functional programming
///     to indicate "no meaningful return value" without using <see langword="void" />.
/// </summary>
[DebuggerDisplay("Unit")]
[JsonConverter(typeof(UnitJsonConverter))]
public readonly struct Unit : IEquatable<Unit>, ISpanFormattable
{
    /// <summary>
    ///     Gets the single <see cref="Unit" /> value.
    /// </summary>
    public static Unit Value { get; } = default;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Unit other)
    {
        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is Unit;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return 0;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return "()";
    }

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return "()";
    }

    /// <inheritdoc />
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

    public static bool operator ==(Unit left, Unit right)
    {
        return true;
    }

    public static bool operator !=(Unit left, Unit right)
    {
        return false;
    }

    /// <summary>
    ///     Implicitly converts from <see cref="Unit" /> to <see cref="ValueTask" />.
    /// </summary>
    public static implicit operator ValueTask(Unit _)
    {
        return default;
    }

    /// <summary>
    ///     Implicitly converts from <see cref="Unit" /> to <see cref="Task" />.
    /// </summary>
    public static implicit operator Task(Unit _)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
///     JSON converter for the Unit type.
/// </summary>
public sealed class UnitJsonConverter : JsonConverter<Unit>
{
    public override Unit Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Null)
        {
            reader.Skip();
        }

        return Unit.Value;
    }

    public override void Write(
        Utf8JsonWriter writer,
        Unit value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteEndObject();
    }
}

/// <summary>
///     Extension methods for Unit type.
/// </summary>
public static class UnitExtensions
{
    /// <summary>
    ///     Converts any value to Unit, effectively discarding it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Unit ToUnit<T>(this T _)
    {
        return Unit.Value;
    }

    /// <summary>
    ///     Asynchronously converts any value to Unit, effectively discarding it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ToUnitAsync<T>(this Task<T> task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }

    /// <summary>
    ///     Asynchronously converts any value to Unit, effectively discarding it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ToUnitAsync<T>(this ValueTask<T> task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }
}
