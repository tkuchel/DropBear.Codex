#region

using Serilog;

#endregion

namespace DropBear.Codex.Core.Logging;

public static class LoggerFactory
{
    private static ILogger _logger = new NoOpLogger();
    private static readonly object Lock = new();

    public static ILogger Logger
    {
        get
        {
            lock (Lock)
            {
                return _logger;
            }
        }
        private set
        {
            lock (Lock)
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
