namespace DropBear.Codex.Utilities.DeepCloning.Attributes;

/// <summary>
///     Specifies the preferred cloning method for a class, allowing for fine-tuned control over the cloning process.
///     This attribute can be applied to classes to override the default cloning behavior.
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
    /// <param name="depth">
    ///     The maximum depth for recursive cloning. Default is 32, which should be sufficient for most types.
    ///     Set to 0 for unlimited depth (use with caution for deeply nested types).
    /// </param>
    public CloneMethodAttribute(bool useExpression = true, int depth = 32)
    {
        UseExpression = useExpression;
        MaxDepth = depth > 0 ? depth : int.MaxValue;
    }

    /// <summary>
    ///     Gets a value indicating whether expression-based cloning should be used.
    /// </summary>
    /// <remarks>
    ///     When true, the class will be cloned using compiled expression trees, which is generally faster
    ///     but may not support all complex types.
    ///     When false, the class will be cloned using JSON serialization, which is more flexible but slower.
    /// </remarks>
    public bool UseExpression { get; }

    /// <summary>
    ///     Gets the maximum recursion depth for cloning this type.
    /// </summary>
    /// <remarks>
    ///     This limits how deep the cloner will traverse object hierarchies when cloning instances
    ///     of this type. This helps prevent stack overflows with deeply nested or circular references.
    /// </remarks>
    public int MaxDepth { get; }

    /// <summary>
    ///     Gets or sets a value indicating whether to skip member validation during cloning.
    /// </summary>
    /// <remarks>
    ///     When true, the cloner will skip validation of properties and fields, which can improve
    ///     performance but may lead to unexpected behavior if invalid data is encountered.
    /// </remarks>
    public bool SkipValidation { get; set; }

    /// <summary>
    ///     Gets or sets the name of a custom cloning method for this type.
    /// </summary>
    /// <remarks>
    ///     When specified, this refers to a static method on the class with the signature:
    ///     static T CustomClone(T source, Dictionary&lt;object, object&gt; track)
    ///     This allows for fully customized cloning logic.
    /// </remarks>
    public string? CustomCloningMethod { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the type should be treated as immutable.
    /// </summary>
    /// <remarks>
    ///     When true, instances of this type will be copied by reference rather than cloned,
    ///     assuming they cannot be modified after creation.
    /// </remarks>
    public bool TreatAsImmutable { get; set; }
}
