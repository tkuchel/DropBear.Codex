#region

using System.Globalization;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

#endregion

namespace DropBear.Codex.Core.Logging;

/// <summary>
///     Provides convenient factory methods and builders for creating Serilog configurations.
///     Optimized for .NET 9 with modern patterns.
/// </summary>
public static class SerilogConfig
{
    /// <summary>
    ///     Gets a basic console logger configuration.
    /// </summary>
    /// <param name="minimumLevel">The minimum log level. Defaults to Information.</param>
    /// <returns>A configured LoggerConfiguration.</returns>
    public static LoggerConfiguration GetConsoleLogger(
        LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName();
    }

    /// <summary>
    ///     Gets a file logger configuration with daily rolling.
    /// </summary>
    /// <param name="filePath">The file path for logs.</param>
    /// <param name="minimumLevel">The minimum log level. Defaults to Debug.</param>
    /// <param name="rollingInterval">The rolling interval. Defaults to Day.</param>
    /// <returns>A configured LoggerConfiguration.</returns>
    public static LoggerConfiguration GetFileLogger(
        string filePath,
        LogEventLevel minimumLevel = LogEventLevel.Debug,
        RollingInterval rollingInterval = RollingInterval.Day)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.File(
                filePath,
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: rollingInterval,
                retainedFileCountLimit: 31,
                buffered: true)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId();
    }

    /// <summary>
    ///     Gets a combined console and file logger configuration.
    /// </summary>
    /// <param name="filePath">The file path for logs.</param>
    /// <param name="minimumLevel">The minimum log level for both sinks.</param>
    /// <returns>A configured LoggerConfiguration.</returns>
    public static LoggerConfiguration GetConsoleAndFileLogger(
        string filePath,
        LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(
                filePath,
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName();
    }

    /// <summary>
    ///     Gets a structured JSON logger configuration for production.
    /// </summary>
    /// <param name="filePath">The file path for logs.</param>
    /// <param name="minimumLevel">The minimum log level.</param>
    /// <returns>A configured LoggerConfiguration.</returns>
    public static LoggerConfiguration GetStructuredLogger(
        string filePath,
        LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.File(
                new CompactJsonFormatter(),
                filePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                buffered: true)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName();
    }

    /// <summary>
    ///     Creates a builder for more complex configurations.
    /// </summary>
    /// <returns>A new SerilogConfigBuilder.</returns>
    public static SerilogConfigBuilder CreateBuilder() => new();
}
