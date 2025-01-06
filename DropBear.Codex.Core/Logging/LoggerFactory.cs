#region

using Serilog;

#endregion

namespace DropBear.Codex.Core.Logging;

/// <summary>
///     A simple factory class to manage the global Serilog <see cref="ILogger" /> instance in a thread-safe manner.
///     By default, it uses a <see cref="NoOpLogger" /> until a real logger is provided.
/// </summary>
public static class LoggerFactory
{
    private static ILogger _logger = new NoOpLogger();
    private static readonly object Lock = new();

    /// <summary>
    ///     Gets the current global <see cref="ILogger" /> instance.
    ///     Defaults to <see cref="NoOpLogger" /> if not set via <see cref="SetLogger" />.
    /// </summary>
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
    ///     Sets the global logger to the specified <paramref name="logger" /> instance.
    ///     Thread-safe, and subsequent calls override the previous logger.
    /// </summary>
    /// <param name="logger">The logger instance to set.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" /> is null.</exception>
    public static void SetLogger(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
