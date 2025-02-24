#region

using Serilog;

#endregion

namespace DropBear.Codex.Core.Logging;

/// <summary>
///     A simple factory class to manage the global Serilog <see cref="ILogger" /> instance.
///     By default, it uses a <see cref="NoOpLogger" /> until a real logger is provided.
///     Thread-safe via volatile reads/writes of the internal logger reference.
/// </summary>
public static class LoggerFactory
{
    /// <summary>
    ///     Holds the current logger instance in a volatile field so that all threads
    ///     immediately see updates made by <see cref="SetLogger" />.
    /// </summary>
    private static ILogger _logger = new NoOpLogger();

    /// <summary>
    ///     Gets the current global <see cref="ILogger" /> instance.
    ///     Defaults to <see cref="NoOpLogger" /> if not set via <see cref="SetLogger" />.
    /// </summary>
    /// <remarks>
    ///     Uses <see cref="Volatile.Read(ref ILogger)" /> to ensure that changes made by
    ///     <see cref="SetLogger" /> are immediately visible to all threads.
    /// </remarks>
    public static ILogger Logger
    {
        get => Volatile.Read(ref _logger);
        private set => Volatile.Write(ref _logger, value);
    }

    /// <summary>
    ///     Sets the global logger to the specified <paramref name="logger" /> instance.
    ///     Thread-safe; subsequent calls override the previous logger globally.
    /// </summary>
    /// <param name="logger">The logger instance to set.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     Uses <see cref="Volatile.Write(ref ILogger, ILogger)" /> to ensure that the update
    ///     is immediately published to all threads.
    /// </remarks>
    public static void SetLogger(ILogger logger)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger), "Logger instance cannot be null.");
        }

        Logger = logger;
    }
}
