using System.Runtime.CompilerServices;
using DropBear.Codex.Utilities.Logging;
using Microsoft.Extensions.Logging;

namespace DropBear.Codex.Utilities.Extensions;

/// <summary>
///     Provides extension methods for <see cref="ILogger"/> with enhanced diagnostic capabilities.
/// </summary>
public static class LoggerExtensions
{
    private static readonly Action<ILogger, string, string, Exception?> TraceWithCallerMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Trace,
            new EventId(0, nameof(LogWithCaller)),
            "[{CallerInfo}] {Message}");

    private static readonly Action<ILogger, string, string, Exception?> DebugWithCallerMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(0, nameof(LogWithCaller)),
            "[{CallerInfo}] {Message}");

    private static readonly Action<ILogger, string, string, Exception?> InformationWithCallerMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(0, nameof(LogWithCaller)),
            "[{CallerInfo}] {Message}");

    private static readonly Action<ILogger, string, string, Exception?> WarningWithCallerMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(0, nameof(LogWithCaller)),
            "[{CallerInfo}] {Message}");

    private static readonly Action<ILogger, string, string, Exception?> ErrorWithCallerMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(0, nameof(LogWithCaller)),
            "[{CallerInfo}] {Message}");

    private static readonly Action<ILogger, string, string, Exception?> CriticalWithCallerMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Critical,
            new EventId(0, nameof(LogWithCaller)),
            "[{CallerInfo}] {Message}");

    /// <summary>
    ///     Logs a message with automatic caller information included for enhanced diagnostics.
    /// </summary>
    /// <typeparam name="TState">The type of the state object to log.</typeparam>
    /// <param name="logger">The ILogger instance to extend.</param>
    /// <param name="logLevel">The log level for this message.</param>
    /// <param name="state">The state object or message to log.</param>
    /// <param name="exception">An optional exception associated with the log entry.</param>
    /// <param name="memberName">
    ///     Automatically populated with the calling member name via <see cref="CallerMemberNameAttribute"/>.
    ///     Do not supply this parameter manually.
    /// </param>
    /// <param name="sourceFilePath">
    ///     Automatically populated with the source file path via <see cref="CallerFilePathAttribute"/>.
    ///     Do not supply this parameter manually.
    /// </param>
    /// <param name="sourceLineNumber">
    ///     Automatically populated with the source line number via <see cref="CallerLineNumberAttribute"/>.
    ///     Do not supply this parameter manually.
    /// </param>
    /// <remarks>
    ///     <para>
    ///     This method automatically captures caller information at compile time using caller information attributes.
    ///     The logged message will be prefixed with "[ClassName.MethodName:LineNumber]" for easy debugging.
    ///     </para>
    ///     <para><strong>Performance</strong>: Caller information is captured at compile time with zero runtime overhead.</para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///     logger.LogWithCaller(LogLevel.Information, "User logged in");
    ///     // Output: [UserService.LoginAsync:42] User logged in
    ///     </code>
    /// </example>
    public static void LogWithCaller<TState>(
        this ILogger logger,
        LogLevel logLevel,
        TState state,
        Exception? exception = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!logger.IsEnabled(logLevel))
        {
            return;
        }

        var callerInfo = CallerLogger.GetCallerInfo(memberName, sourceFilePath, sourceLineNumber);
        var message = state?.ToString() ?? string.Empty;

        switch (logLevel)
        {
            case LogLevel.Trace:
                TraceWithCallerMessage(logger, callerInfo, message, exception);
                break;
            case LogLevel.Debug:
                DebugWithCallerMessage(logger, callerInfo, message, exception);
                break;
            case LogLevel.Information:
                InformationWithCallerMessage(logger, callerInfo, message, exception);
                break;
            case LogLevel.Warning:
                WarningWithCallerMessage(logger, callerInfo, message, exception);
                break;
            case LogLevel.Error:
                ErrorWithCallerMessage(logger, callerInfo, message, exception);
                break;
            case LogLevel.Critical:
                CriticalWithCallerMessage(logger, callerInfo, message, exception);
                break;
            case LogLevel.None:
            default:
                break;
        }
    }
}
