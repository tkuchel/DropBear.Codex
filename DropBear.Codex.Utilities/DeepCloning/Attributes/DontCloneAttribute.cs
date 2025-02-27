namespace DropBear.Codex.Utilities.DeepCloning.Attributes;

/// <summary>
///     Indicates that a field or property should be excluded from deep cloning operations.
///     This is a convenience attribute that is equivalent to [Cloneable(false)].
/// </summary>
/// <remarks>
///     When this attribute is applied to a member, its value will not be copied during cloning.
///     For reference types, null will be assigned. For value types, the default value will be used.
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class DontCloneAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DontCloneAttribute" /> class.
    /// </summary>
    /// <remarks>
    ///     This attribute is simply a more readable alternative to [Cloneable(false)].
    /// </remarks>
    public DontCloneAttribute()
    {
    }

    /// <summary>
    ///     Gets or sets a custom message explaining why this member should not be cloned.
    /// </summary>
    /// <remarks>
    ///     This is for documentation purposes only and does not affect the cloning behavior.
    /// </remarks>
    public string? Reason { get; set; }
}
