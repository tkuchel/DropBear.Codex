#region

using System.Runtime.Serialization;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Exception thrown when a Result operation encounters an error.
///     Optimized for .NET 9 with modern exception patterns.
/// </summary>
[Serializable]
public class ResultException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ResultException"/> class.
    /// </summary>
    public ResultException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ResultException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ResultException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ResultException"/> class with a specified error message
    ///     and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ResultException(string message, Exception innerException) : base(message, innerException)
    {
    }

#if NET8_0_OR_GREATER
    /// <summary>
    ///     Initializes a new instance of the <see cref="ResultException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data.</param>
    /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information.</param>
    [Obsolete("This API supports obsolete formatter-based serialization.", DiagnosticId = "SYSLIB0051")]
#endif
    protected ResultException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
