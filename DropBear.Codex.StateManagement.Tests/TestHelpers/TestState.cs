using DropBear.Codex.StateManagement.StateSnapshots.Interfaces;

namespace DropBear.Codex.StateManagement.Tests.TestHelpers;

/// <summary>
/// Test state implementation for snapshot testing
/// </summary>
public sealed class TestState : ICloneable<TestState>
{
    public string Name { get; init; }
    public int Version { get; init; }
    public DateTime Timestamp { get; init; }

    public TestState(string name, int version, DateTime timestamp)
    {
        Name = name;
        Version = version;
        Timestamp = timestamp;
    }

    public TestState Clone() => new(Name, Version, Timestamp);

    public static TestState Create() => new("Initial", 1, DateTime.UtcNow);
    public TestState WithName(string name) => new(name, Version, Timestamp);
    public TestState WithVersion(int version) => new(Name, version, Timestamp);

    // Override Equals and GetHashCode for proper comparison
    public override bool Equals(object? obj) =>
        obj is TestState other && Name == other.Name && Version == other.Version;

    public override int GetHashCode() => HashCode.Combine(Name, Version);
}

/// <summary>
/// Simple test state for basic scenarios
/// </summary>
public sealed class SimpleTestState : ICloneable<SimpleTestState>
{
    public int Value { get; init; }

    public SimpleTestState(int value)
    {
        Value = value;
    }

    public SimpleTestState Clone() => new(Value);

    public override bool Equals(object? obj) =>
        obj is SimpleTestState other && Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();
}
