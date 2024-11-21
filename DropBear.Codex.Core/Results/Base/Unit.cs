namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Represents a void type for functional programming patterns
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    public static Unit Value { get; } = new();

    public bool Equals(Unit other)
    {
        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is Unit;
    }

    public override int GetHashCode()
    {
        return 0;
    }

    public override string ToString()
    {
        return "()";
    }

    public static bool operator ==(Unit left, Unit right)
    {
        return true;
    }

    public static bool operator !=(Unit left, Unit right)
    {
        return false;
    }
}
