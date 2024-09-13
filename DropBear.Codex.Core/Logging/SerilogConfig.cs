#region

using Serilog;
using Serilog.Events;

#endregion

namespace DropBear.Codex.Core.Logging;

public static class SerilogConfig
{
    // Example usage:
    // Log.Logger = SerilogConfig.GetConsoleAndFileLogger(@"C:\Logs\myapp-log.txt").CreateLogger();
    // Log.Logger = SerilogConfig.GetFileLogger(@"C:\Logs\myapp-log-.txt").CreateLogger();

    // Default console logger configuration
    public static LoggerConfiguration GetConsoleLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information() // Default to Information, but can be changed
            .WriteTo.Console()
            .Enrich.FromLogContext(); // Add context enrichment (optional)
    }

    // File logger configuration with daily rolling interval
    public static LoggerConfiguration GetFileLogger(string filePath)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug() // Default to Debug for detailed logs
            .WriteTo.File(filePath, rollingInterval: RollingInterval.Day)
            .Enrich.FromLogContext();
    }

    // Combined console and file logger with customizable log level
    public static LoggerConfiguration GetConsoleAndFileLogger(string filePath,
        LogEventLevel logLevel = LogEventLevel.Information)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Is(logLevel) // Use the passed log level
            .WriteTo.Console()
            .WriteTo.File(filePath, rollingInterval: RollingInterval.Day)
            .Enrich.FromLogContext();
    }

    // Advanced logger configuration with enriching properties
    public static LoggerConfiguration GetAdvancedConsoleAndFileLogger(string filePath)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(filePath, rollingInterval: RollingInterval.Day)
            .Enrich.FromLogContext();
    }
}
