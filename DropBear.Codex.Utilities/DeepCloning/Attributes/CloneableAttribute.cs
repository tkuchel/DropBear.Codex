namespace DropBear.Codex.Utilities.DeepCloning.Attributes;

/// <summary>
///     Indicates whether a field or property should be included in deep cloning operations.
///     This attribute can be used to fine-tune the cloning process for specific members.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class CloneableAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CloneableAttribute" /> class.
    /// </summary>
    /// <param name="isCloneable">
    ///     If set to <see langword="true" />, the member is considered cloneable;
    ///     otherwise, it is excluded from cloning.
    /// </param>
    /// <param name="priority">
    ///     Optional priority for cloning. Higher priority members are cloned first.
    ///     Useful when some properties depend on others being initialized first.
    /// </param>
    public CloneableAttribute(bool isCloneable = true, int priority = 0)
    {
        IsCloneable = isCloneable;
        Priority = priority;
    }

    /// <summary>
    ///     Gets a value indicating whether the member is cloneable.
    /// </summary>
    /// <remarks>
    ///     When set to false, the member will be excluded from the cloning process.
    ///     For reference types, null will be assigned. For value types, the default value will be used.
    /// </remarks>
    public bool IsCloneable { get; }

    /// <summary>
    ///     Gets the priority of this member during cloning.
    /// </summary>
    /// <remarks>
    ///     Members with higher priority values are cloned before those with lower values.
    ///     This can be useful when some properties depend on others being initialized first.
    /// </remarks>
    public int Priority { get; }

    /// <summary>
    ///     Gets or sets a value indicating whether to perform a shallow copy of the member
    ///     even if the member itself is a complex type that would normally be deep cloned.
    /// </summary>
    /// <remarks>
    ///     When set to true, the member will be copied as-is without recursively cloning its members.
    ///     This is useful for types that shouldn't be deep cloned, such as external system resources.
    /// </remarks>
    public bool ShallowCopyOnly { get; set; }

    /// <summary>
    ///     Gets or sets custom cloning behavior.
    /// </summary>
    /// <remarks>
    ///     When specified, this refers to a method name that will be used to clone this member.
    ///     The method should be a static method on the declaring type with the signature:
    ///     static T CloneMethod(T source, Dictionary&lt;object, object&gt; track)
    /// </remarks>
    public string? CustomCloningMethod { get; set; }
}
