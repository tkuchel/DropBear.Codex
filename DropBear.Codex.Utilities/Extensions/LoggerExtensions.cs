using System.Runtime.CompilerServices;
using DropBear.Codex.Utilities.Logging;
using Microsoft.Extensions.Logging;

namespace DropBear.Codex.Utilities.Extensions;

public static class LoggerExtensions
{
    /// <summary>
    /// Extends ILogger to include caller information in logs.
    /// </summary>
    public static void LogWithCaller<TState>(
        this ILogger logger,
        LogLevel logLevel,
        TState state,
        Exception? exception = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        var callerInfo = CallerLogger.GetCallerInfo(memberName, sourceFilePath, sourceLineNumber);
        logger.Log(
            logLevel,
            exception,
            $"[{callerInfo}] {state}");
    }
}
