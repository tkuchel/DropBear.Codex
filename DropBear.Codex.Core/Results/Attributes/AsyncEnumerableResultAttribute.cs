namespace DropBear.Codex.Core.Results.Attributes;

/// <summary>
///     Marks a result type as supporting async enumeration pattern.
///     Used for compile-time validation and tooling support.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class AsyncEnumerableResultAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance with the enumerable type.
    /// </summary>
    /// <param name="enumerableType">The type that implements IAsyncEnumerable.</param>
    public AsyncEnumerableResultAttribute(Type enumerableType)
    {
        ArgumentNullException.ThrowIfNull(enumerableType);
        EnumerableType = enumerableType;
    }

    /// <summary>
    ///     Gets the enumerable type.
    /// </summary>
    public Type EnumerableType { get; }

    /// <summary>
    ///     Gets a value indicating whether this is a generic enumerable type.
    /// </summary>
    public bool IsGeneric => EnumerableType.IsGenericType;
}
