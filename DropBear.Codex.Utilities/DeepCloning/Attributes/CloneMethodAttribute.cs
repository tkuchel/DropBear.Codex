/// <summary>
///     Specifies the preferred cloning method for a class, allowing for fine-tuned control over the cloning process.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CloneMethodAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CloneMethodAttribute" /> class with a specified cloning method
    ///     preference.
    /// </summary>
    /// <param name="useExpression">
    ///     If set to <see langword="true" />, expression-based cloning is used; otherwise, JSON-based
    ///     cloning is used.
    /// </param>
    public CloneMethodAttribute(bool useExpression = true)
    {
        UseExpression = useExpression;
    }

    /// <summary>
    ///     Gets a value indicating whether expression-based cloning should be used.
    /// </summary>
    public bool UseExpression { get; }
}
