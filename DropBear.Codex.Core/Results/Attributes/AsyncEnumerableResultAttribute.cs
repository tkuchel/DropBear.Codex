namespace DropBear.Codex.Core.Results.Attributes;

/// <summary>
///     Marks a result type as supporting custom async enumeration pattern.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AsyncEnumerableResultAttribute : Attribute
{
    public AsyncEnumerableResultAttribute(Type enumerableType)
    {
        EnumerableType = enumerableType;
    }

    public Type EnumerableType { get; }
}
