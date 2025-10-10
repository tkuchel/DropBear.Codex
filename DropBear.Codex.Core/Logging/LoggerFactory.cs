#region

using Serilog;
#pragma warning disable CS1580 // Invalid type for parameter in XML comment cref attribute

#endregion

namespace DropBear.Codex.Core.Logging;

/// <summary>
///     A simple factory that manages the global Serilog <see cref="ILogger" /> instance.
///     Defaults to <see cref="NoOpLogger" /> until a real logger is supplied.
/// </summary>
/// <remarks>
///     Thread-safety is achieved via <see cref="Volatile.Read{T}(ref T)" /> and
///     <see cref="Volatile.Write{T}(ref T, T)" /> on the backing field, ensuring visibility
///     of updates across threads without locks.
/// </remarks>
public static class LoggerFactory
{
    /// <summary>
    ///     Backing field for the current logger.
    ///     Initialized with a <see cref="NoOpLogger" /> to avoid null checks at call sites.
    /// </summary>
    private static ILogger _logger = new NoOpLogger();

    /// <summary>
    ///     Gets the current global <see cref="ILogger" /> instance.
    ///     If not set explicitly, this returns a <see cref="NoOpLogger" />.
    /// </summary>
    /// <remarks>
    ///     Uses <see cref="Volatile.Read{T}(ref T)" /> to ensure that changes made by
    ///     <see cref="SetLogger(ILogger)" /> are immediately visible to all threads.
    /// </remarks>
    public static ILogger Logger
    {
        get => Volatile.Read(ref _logger);
        private set => Volatile.Write(ref _logger, value);
    }

    /// <summary>
    ///     Sets the global logger to the specified <paramref name="logger" /> instance.
    ///     Subsequent reads via <see cref="Logger" /> will observe this value.
    /// </summary>
    /// <param name="logger">The logger instance to set.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     Uses <see cref="Volatile.Write{T}(ref T, T)" /> so the update is immediately
    ///     published to all threads.
    /// </remarks>
    public static void SetLogger(ILogger logger) => Logger =
        logger ?? throw new ArgumentNullException(nameof(logger), "Logger instance cannot be null.");
}
