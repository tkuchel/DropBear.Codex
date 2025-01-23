#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

#endregion

namespace DropBear.Codex.Utilities.Logging;

/// <summary>
///     Provides utility methods for logging caller information in a high-performance manner.
/// </summary>
public static class CallerLogger
{
    /// <summary>
    ///     Gets the calling method information including class name, method name, and line number.
    ///     Uses compiler-generated caller information attributes for accuracy and performance.
    /// </summary>
    /// <param name="memberName">Automatically populated with calling method name</param>
    /// <param name="sourceFilePath">Automatically populated with source file path</param>
    /// <param name="sourceLineNumber">Automatically populated with line number</param>
    /// <returns>A formatted string containing caller information</returns>
    public static string GetCallerInfo(
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        // Use Path.GetFileName to avoid exposing full file path in logs
        var fileName = Path.GetFileName(sourceFilePath);
        return $"{fileName}:{memberName}:Line {sourceLineNumber}";
    }

    /// <summary>
    ///     Gets detailed caller information including stack trace details.
    ///     Note: This method has more overhead due to stack trace generation.
    /// </summary>
    /// <returns>Detailed caller information including stack frame details</returns>
    public static string GetDetailedCallerInfo()
    {
        var stackTrace = new StackTrace(true);
        var frame = stackTrace.GetFrame(1); // Skip current method frame

        if (frame == null)
        {
            return "Stack frame information unavailable";
        }

        return new StringBuilder()
            .Append(frame.GetFileName() ?? "Unknown File")
            .Append(":")
            .Append(frame.GetMethod()?.Name ?? "Unknown Method")
            .Append(":Line ")
            .Append(frame.GetFileLineNumber())
            .ToString();
    }
}
