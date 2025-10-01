#region

using System.Diagnostics;
using System.Runtime.Serialization;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Exception thrown during result transformation operations.
///     Provides specific context about transformation failures.
/// </summary>
[DebuggerDisplay("ResultTransformationException: {Message}")]
[Serializable]
public sealed class ResultTransformationException : ResultException
{
    /// <summary>
    ///     Initializes a new instance of ResultTransformationException.
    /// </summary>
    public ResultTransformationException()
        : base("Result transformation failed")
    {
        Severity = ErrorSeverity.Medium;
    }

    /// <summary>
    ///     Initializes a new instance with a message.
    /// </summary>
    public ResultTransformationException(string message)
        : base(message)
    {
        Severity = ErrorSeverity.Medium;
    }

    /// <summary>
    ///     Initializes a new instance with a message and inner exception.
    /// </summary>
    public ResultTransformationException(string message, Exception inner)
        : base(message, inner)
    {
        Severity = ErrorSeverity.Medium;
    }

#if NET8_0_OR_GREATER
    [Obsolete("This API supports obsolete formatter-based serialization.", DiagnosticId = "SYSLIB0051")]
#endif
    private ResultTransformationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        SourceType = info.GetString(nameof(SourceType));
        TargetType = info.GetString(nameof(TargetType));
        TransformationStep = info.GetString(nameof(TransformationStep));
    }

    /// <summary>
    ///     Gets the source type being transformed from.
    /// </summary>
    public string? SourceType { get; init; }

    /// <summary>
    ///     Gets the target type being transformed to.
    /// </summary>
    public string? TargetType { get; init; }

    /// <summary>
    ///     Gets the transformation step where the failure occurred.
    /// </summary>
    public string? TransformationStep { get; init; }

    /// <summary>
    ///     Creates a transformation exception with type information.
    /// </summary>
    public static ResultTransformationException ForTransformation<TSource, TTarget>(
        string step,
        string? message = null,
        Exception? innerException = null)
    {
        var defaultMessage = $"Failed to transform from {typeof(TSource).Name} to {typeof(TTarget).Name} at step: {step}";

        return new ResultTransformationException(message ?? defaultMessage, innerException!)
        {
            SourceType = typeof(TSource).FullName,
            TargetType = typeof(TTarget).FullName,
            TransformationStep = step,
            OperationName = $"Transform<{typeof(TSource).Name}, {typeof(TTarget).Name}>"
        };
    }

    /// <summary>
    ///     Creates a transformation exception for a mapping operation.
    /// </summary>
    public static ResultTransformationException ForMapping<TSource, TTarget>(
        Exception innerException)
    {
        return ForTransformation<TSource, TTarget>(
            "Map",
            $"Mapping function threw an exception",
            innerException);
    }

    /// <summary>
    ///     Creates a transformation exception for a binding operation.
    /// </summary>
    public static ResultTransformationException ForBinding<TSource, TTarget>(
        Exception innerException)
    {
        return ForTransformation<TSource, TTarget>(
            "Bind",
            $"Binding function threw an exception",
            innerException);
    }

#if NET8_0_OR_GREATER
    [Obsolete("This API supports obsolete formatter-based serialization.", DiagnosticId = "SYSLIB0051")]
#endif
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        ArgumentNullException.ThrowIfNull(info);

        base.GetObjectData(info, context);
        info.AddValue(nameof(SourceType), SourceType);
        info.AddValue(nameof(TargetType), TargetType);
        info.AddValue(nameof(TransformationStep), TransformationStep);
    }
}
