#region

using Serilog;
using Serilog.Events;

#endregion

namespace DropBear.Codex.Core.Logging;

/// <summary>
///     Provides convenient factory methods for creating common Serilog <see cref="LoggerConfiguration" /> setups,
///     such as console logging, file logging, or combined console-and-file logging.
/// </summary>
public static class SerilogConfig
{
    /// <summary>
    ///     Gets a basic console logger configuration with a default minimum level of <see cref="LogEventLevel.Information" />.
    /// </summary>
    /// <returns>A <see cref="LoggerConfiguration" /> for console logging.</returns>
    public static LoggerConfiguration GetConsoleLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information() // Default
            .WriteTo.Console()
            .Enrich.FromLogContext();
    }

    /// <summary>
    ///     Creates a file logger configuration with daily rolling interval
    ///     and a default minimum level of <see cref="LogEventLevel.Debug" />.
    /// </summary>
    /// <param name="filePath">The file path for the log file (supports rolling pattern).</param>
    /// <returns>A <see cref="LoggerConfiguration" /> that logs to a file.</returns>
    public static LoggerConfiguration GetFileLogger(string filePath)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(filePath, rollingInterval: RollingInterval.Day)
            .Enrich.FromLogContext();
    }

    /// <summary>
    ///     Creates a combined console and file logger configuration.
    ///     Allows specifying the <paramref name="logLevel" /> for both sinks.
    /// </summary>
    /// <param name="filePath">The file path for the log file.</param>
    /// <param name="logLevel">The minimum <see cref="LogEventLevel" /> for both console and file logs.</param>
    /// <returns>A <see cref="LoggerConfiguration" /> for a combined console and file logger.</returns>
    public static LoggerConfiguration GetConsoleAndFileLogger(
        string filePath,
        LogEventLevel logLevel = LogEventLevel.Information)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .WriteTo.Console()
            .WriteTo.File(filePath, rollingInterval: RollingInterval.Day)
            .Enrich.FromLogContext();
    }

    /// <summary>
    ///     Creates a more advanced console and file logger configuration with a default min level of
    ///     <see cref="LogEventLevel.Debug" />.
    ///     Useful if you want more detailed logs while also writing to a file.
    /// </summary>
    /// <param name="filePath">The file path for the log file.</param>
    /// <returns>A <see cref="LoggerConfiguration" /> for an advanced console and file logger.</returns>
    public static LoggerConfiguration GetAdvancedConsoleAndFileLogger(string filePath)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(filePath, rollingInterval: RollingInterval.Day)
            .Enrich.FromLogContext();
    }
}
