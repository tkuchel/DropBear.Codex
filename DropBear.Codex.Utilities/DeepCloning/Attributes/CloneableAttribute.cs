namespace DropBear.Codex.Utilities.DeepCloning.Attributes;

/// <summary>
///     Indicates whether a field should be included in deep cloning operations.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class CloneableAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CloneableAttribute" /> class.
    /// </summary>
    /// <param name="isCloneable">If set to <see langword="true" />, the field is considered cloneable; otherwise, it is not.</param>
    public CloneableAttribute(bool isCloneable = true)
    {
        IsCloneable = isCloneable;
    }

    /// <summary>
    ///     Gets a value indicating whether the field is cloneable.
    /// </summary>
    public bool IsCloneable { get; }
}
