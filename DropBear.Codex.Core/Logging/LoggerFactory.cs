#region

using Serilog;

#endregion

namespace DropBear.Codex.Core.Logging;

public static class LoggerFactory
{
    private static ILogger _logger = new NoOpLogger();
    private static readonly object _lock = new();

    public static ILogger Logger
    {
        get
        {
            lock (_lock)
            {
                return _logger;
            }
        }
        private set
        {
            lock (_lock)
            {
                _logger = value;
            }
        }
    }

    /// <summary>
    ///     Sets the global logger to the specified logger instance.
    /// </summary>
    /// <param name="logger">The logger instance to set.</param>
    public static void SetLogger(ILogger logger)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        Logger = logger;
    }
}
