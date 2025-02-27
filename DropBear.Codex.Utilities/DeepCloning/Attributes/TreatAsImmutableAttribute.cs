namespace DropBear.Codex.Utilities.DeepCloning.Attributes;

/// <summary>
///     Indicates that a type should be treated as immutable during deep cloning operations.
///     Immutable types are copied by reference rather than performing a deep clone.
/// </summary>
/// <remarks>
///     This attribute is useful for types that are semantically immutable but not recognized
///     as such by the default immutability detection logic in <see cref="DeepCloner" />.
///     Types marked with this attribute will be copied by reference without recursively
///     cloning their members, which can improve performance and prevent unnecessary copying.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class TreatAsImmutableAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TreatAsImmutableAttribute" /> class.
    /// </summary>
    public TreatAsImmutableAttribute()
    {
    }

    /// <summary>
    ///     Gets or sets a custom message explaining why this type should be treated as immutable.
    /// </summary>
    /// <remarks>
    ///     This is for documentation purposes only and does not affect the cloning behavior.
    /// </remarks>
    public string? Reason { get; set; }
}
