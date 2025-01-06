namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Represents a type that has a single possible value, commonly used in functional programming
///     to indicate "no meaningful return value" without using <see langword="void" />.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    ///     Gets the single <see cref="Unit" /> value.
    /// </summary>
    public static Unit Value { get; } = new();

    /// <inheritdoc />
    public bool Equals(Unit other)
    {
        // All Unit instances are logically equivalent.
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
        // All Unit instances have the same hash code, e.g. 0.
        return 0;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return "()";
    }

    /// <summary>
    ///     Equality operator for <see cref="Unit" />; always returns true.
    /// </summary>
    public static bool operator ==(Unit left, Unit right)
    {
        return true;
    }

    /// <summary>
    ///     Inequality operator for <see cref="Unit" />; always returns false.
    /// </summary>
    public static bool operator !=(Unit left, Unit right)
    {
        return false;
    }
}
