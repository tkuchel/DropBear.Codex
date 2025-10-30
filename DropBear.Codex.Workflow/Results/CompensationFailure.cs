namespace DropBear.Codex.Workflow.Results;

/// <summary>
///     Represents a failure that occurred during workflow compensation (Saga pattern rollback).
/// </summary>
/// <param name="StepName">The name of the step that failed to compensate</param>
/// <param name="ErrorMessage">The error message describing the failure</param>
/// <param name="SourceException">The exception that caused the compensation failure, if any</param>
public sealed record CompensationFailure(
    string StepName,
    string ErrorMessage,
    Exception? SourceException = null)
{
    /// <summary>
    ///     Gets the full error message including exception details.
    /// </summary>
    public string GetFullErrorMessage()
    {
        if (SourceException is null)
        {
            return ErrorMessage;
        }

        var messages = new List<string> { ErrorMessage };
        var currentException = SourceException;

        while (currentException is not null)
        {
            messages.Add(currentException.Message);
            currentException = currentException.InnerException;
        }

        return string.Join(" -> ", messages);
    }
}
