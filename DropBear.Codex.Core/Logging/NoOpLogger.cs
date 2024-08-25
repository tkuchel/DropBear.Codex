#region

using DropBear.Codex.Core.Interfaces;

#endregion

namespace DropBear.Codex.Core.Logging;

public class NoOpLogger : ILogger
{
    /// <summary>
    ///     Logs an error with the specified exception and message. This implementation does nothing.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="message">The message to log.</param>
    public void LogError(Exception ex, string message)
    {
        // No operation performed
    }

    /// <summary>
    ///     Logs a debug message. This implementation does nothing.
    /// </summary>
    /// <param name="message">The debug message to log.</param>
    public void LogDebug(string message)
    {
        // No operation performed
    }

    /// <summary>
    ///     Logs an informational message. This implementation does nothing.
    /// </summary>
    /// <param name="message">The informational message to log.</param>
    public void LogInformation(string message)
    {
        // No operation performed
    }

    /// <summary>
    ///     Logs a warning message. This implementation does nothing.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    public void LogWarning(string message)
    {
        // No operation performed
    }

    /// <summary>
    ///     Logs a critical message. This implementation does nothing.
    /// </summary>
    /// <param name="message">The critical message to log.</param>
    public void LogCritical(string message)
    {
        // No operation performed
    }
}
