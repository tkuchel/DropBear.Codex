#region

using System.Runtime.Serialization;

#endregion

namespace DropBear.Codex.Blazor.Exceptions;

/// <summary>
///     Represents errors that occur during snackbar operations in the application.
/// </summary>
[Serializable]
public sealed class SnackbarException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SnackbarException" /> class.
    /// </summary>
    public SnackbarException() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SnackbarException" /> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public SnackbarException(string message) : base(message) { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SnackbarException" /> class with a specified error message and a
    ///     reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">
    ///     The exception that is the cause of the current exception, or a null reference if no inner
    ///     exception is specified.
    /// </param>
    public SnackbarException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SnackbarException" /> class with serialized data.
    /// </summary>
    /// <param name="info">
    ///     The <see cref="SerializationInfo" /> that holds the serialized object data about the exception being
    ///     thrown.
    /// </param>
    /// <param name="context">
    ///     The <see cref="StreamingContext" /> that contains contextual information about the source or
    ///     destination.
    /// </param>
    [Obsolete("Formatter-based serialization is obsolete and should not be used.", true)]
    private SnackbarException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }

    /// <summary>
    ///     Sets the <see cref="SerializationInfo" /> with information about the exception.
    /// </summary>
    /// <param name="info">
    ///     The <see cref="SerializationInfo" /> that holds the serialized object data about the exception being
    ///     thrown.
    /// </param>
    /// <param name="context">
    ///     The <see cref="StreamingContext" /> that contains contextual information about the source or
    ///     destination.
    /// </param>
    [Obsolete("Formatter-based serialization is obsolete and should not be used.", true)]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
    }
}
