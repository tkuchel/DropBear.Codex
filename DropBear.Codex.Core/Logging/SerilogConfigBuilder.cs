#region

using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

#endregion

namespace DropBear.Codex.Core.Logging;

/// <summary>
///     Builder for creating custom Serilog configurations.
/// </summary>
public sealed class SerilogConfigBuilder
{
    private readonly LoggerConfiguration _configuration;
    private bool _enableConsole;
    private bool _enableFile;
    private bool _enrichWithEnvironment;
    private bool _enrichWithMachineName;
    private bool _enrichWithProcessId;
    private bool _enrichWithThreadId = true;
    private string? _filePath;
    private LogEventLevel _minimumLevel = LogEventLevel.Information;
    private RollingInterval _rollingInterval = RollingInterval.Day;
    private bool _useStructuredLogging;

    internal SerilogConfigBuilder()
    {
        _configuration = new LoggerConfiguration();
    }

    /// <summary>
    ///     Sets the minimum log level.
    /// </summary>
    public SerilogConfigBuilder WithMinimumLevel(LogEventLevel level)
    {
        _minimumLevel = level;
        return this;
    }

    /// <summary>
    ///     Enables console logging.
    /// </summary>
    public SerilogConfigBuilder WithConsole()
    {
        _enableConsole = true;
        return this;
    }

    /// <summary>
    ///     Enables file logging.
    /// </summary>
    public SerilogConfigBuilder WithFile(
        string filePath,
        RollingInterval rollingInterval = RollingInterval.Day)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _enableFile = true;
        _filePath = filePath;
        _rollingInterval = rollingInterval;
        return this;
    }

    /// <summary>
    ///     Enables structured JSON logging.
    /// </summary>
    public SerilogConfigBuilder WithStructuredLogging()
    {
        _useStructuredLogging = true;
        return this;
    }

    /// <summary>
    ///     Configures enrichment options.
    /// </summary>
    public SerilogConfigBuilder WithEnrichment(
        bool threadId = true,
        bool processId = false,
        bool machineName = false,
        bool environment = false)
    {
        _enrichWithThreadId = threadId;
        _enrichWithProcessId = processId;
        _enrichWithMachineName = machineName;
        _enrichWithEnvironment = environment;
        return this;
    }

    /// <summary>
    ///     Builds the LoggerConfiguration.
    /// </summary>
    public LoggerConfiguration Build()
    {
        _configuration.MinimumLevel.Is(_minimumLevel);
        _configuration.Enrich.FromLogContext();

        if (_enrichWithThreadId)
        {
            _configuration.Enrich.WithThreadId();
        }

        if (_enrichWithProcessId)
        {
            _configuration.Enrich.WithProcessId();
        }

        if (_enrichWithMachineName)
        {
            _configuration.Enrich.WithMachineName();
        }

        if (_enrichWithEnvironment)
        {
            _configuration.Enrich.WithEnvironmentName();
        }

        if (_enableConsole)
        {
            _configuration.WriteTo.Console();
        }

        if (_enableFile && !string.IsNullOrWhiteSpace(_filePath))
        {
            if (_useStructuredLogging)
            {
                _configuration.WriteTo.File(
                    new CompactJsonFormatter(),
                    _filePath,
                    rollingInterval: _rollingInterval,
                    retainedFileCountLimit: 31,
                    buffered: true);
            }
            else
            {
                _configuration.WriteTo.File(
                    _filePath,
                    rollingInterval: _rollingInterval,
                    retainedFileCountLimit: 31,
                    buffered: true);
            }
        }

        return _configuration;
    }
}
