namespace DropBear.Codex.Tasks.OperationManagement;

/// <summary>
///     Provides data for the OperationFailed event.
/// </summary>
public class OperationFailedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the OperationFailedEventArgs class.
    /// </summary>
    /// <param name="exception">The exception that caused the operation to fail.</param>
    public OperationFailedEventArgs(Exception exception)
    {
        Exception = exception;
    }

    /// <summary>
    ///     Gets the exception that caused the operation to fail.
    /// </summary>
    public Exception Exception { get; }
}
