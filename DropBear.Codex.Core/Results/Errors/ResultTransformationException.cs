#region

using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Exception thrown when a result transformation fails.
///     Optimized for .NET 9 with modern exception patterns.
/// </summary>
public class ResultTransformationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of ResultTransformationException.
    /// </summary>
    public ResultTransformationException()
        : base("Result transformation failed")
    {
    }

    /// <summary>
    ///     Initializes a new instance with a message.
    /// </summary>
    public ResultTransformationException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance with a message and inner exception.
    /// </summary>
    public ResultTransformationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance with result state information.
    /// </summary>
    public ResultTransformationException(
        string message,
        ResultState sourceState,
        Type sourceType,
        Type targetType)
        : base(message)
    {
        SourceState = sourceState;
        SourceType = sourceType;
        TargetType = targetType;
    }

    /// <summary>
    ///     Initializes a new instance with result state information and inner exception.
    /// </summary>
    public ResultTransformationException(
        string message,
        ResultState sourceState,
        Type sourceType,
        Type targetType,
        Exception innerException)
        : base(message, innerException)
    {
        SourceState = sourceState;
        SourceType = sourceType;
        TargetType = targetType;
    }

    /// <summary>
    ///     Gets the state of the source result.
    /// </summary>
    public ResultState? SourceState { get; }

    /// <summary>
    ///     Gets the type of the source result.
    /// </summary>
    public Type? SourceType { get; }

    /// <summary>
    ///     Gets the type of the target result.
    /// </summary>
    public Type? TargetType { get; }

    #region Factory Methods

    /// <summary>
    ///     Creates a ResultTransformationException for a mapping failure.
    /// </summary>
    public static ResultTransformationException MapFailed<TSource, TTarget>(
        string? customMessage = null,
        Exception? innerException = null)
    {
        var message = customMessage ??
                      $"Failed to map from {typeof(TSource).Name} to {typeof(TTarget).Name}";

        return innerException is null
            ? new ResultTransformationException(
                message,
                ResultState.Failure,
                typeof(TSource),
                typeof(TTarget))
            : new ResultTransformationException(
                message,
                ResultState.Failure,
                typeof(TSource),
                typeof(TTarget),
                innerException);
    }

    /// <summary>
    ///     Creates a ResultTransformationException for a bind failure.
    /// </summary>
    public static ResultTransformationException BindFailed<TSource, TTarget>(
        ResultState sourceState,
        string? customMessage = null,
        Exception? innerException = null)
    {
        var message = customMessage ??
                      $"Failed to bind from {typeof(TSource).Name} (State: {sourceState}) to {typeof(TTarget).Name}";

        return innerException is null
            ? new ResultTransformationException(
                message,
                sourceState,
                typeof(TSource),
                typeof(TTarget))
            : new ResultTransformationException(
                message,
                sourceState,
                typeof(TSource),
                typeof(TTarget),
                innerException);
    }

    /// <summary>
    ///     Creates a ResultTransformationException for a conversion failure.
    /// </summary>
    public static ResultTransformationException ConversionFailed<TSource, TTarget>(
        string? customMessage = null,
        Exception? innerException = null)
    {
        var message = customMessage ??
                      $"Failed to convert from {typeof(TSource).Name} to {typeof(TTarget).Name}";

        return innerException is null
            ? new ResultTransformationException(
                message,
                ResultState.Failure,
                typeof(TSource),
                typeof(TTarget))
            : new ResultTransformationException(
                message,
                ResultState.Failure,
                typeof(TSource),
                typeof(TTarget),
                innerException);
    }

    /// <summary>
    ///     Creates a ResultTransformationException for an invalid state transition.
    /// </summary>
    public static ResultTransformationException InvalidStateTransition(
        ResultState sourceState,
        ResultState targetState,
        Type resultType)
    {
        var message = $"Invalid state transition from {sourceState} to {targetState} for type {resultType.Name}";

        return new ResultTransformationException(
            message,
            sourceState,
            resultType,
            resultType);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    ///     Gets a detailed error message including type information.
    /// </summary>
    public string GetDetailedMessage()
    {
        var details = new List<string> { Message };

        if (SourceState.HasValue)
        {
            details.Add($"Source State: {SourceState.Value}");
        }

        if (SourceType is not null)
        {
            details.Add($"Source Type: {SourceType.Name}");
        }

        if (TargetType is not null)
        {
            details.Add($"Target Type: {TargetType.Name}");
        }

        if (InnerException is not null)
        {
            details.Add($"Inner Exception: {InnerException.Message}");
        }

        return string.Join(Environment.NewLine, details);
    }

    /// <summary>
    ///     Gets transformation metadata as a dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, object?> GetMetadata()
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Message"] = Message,
            ["SourceState"] = SourceState?.ToString(),
            ["SourceType"] = SourceType?.Name,
            ["TargetType"] = TargetType?.Name,
            ["HasInnerException"] = InnerException is not null
        };

        if (InnerException is not null)
        {
            metadata["InnerExceptionType"] = InnerException.GetType().Name;
            metadata["InnerExceptionMessage"] = InnerException.Message;
        }

        return metadata;
    }

    #endregion
}
