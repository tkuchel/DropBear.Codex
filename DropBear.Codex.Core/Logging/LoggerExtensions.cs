#region

using Serilog;

#endregion

namespace DropBear.Codex.Core.Logging;

public static class LoggerExtensions
{
    /// <summary>
    ///     Allows specifying a static class as the context for logging.
    /// </summary>
    /// <param name="logger">The base logger.</param>
    /// <param name="staticClass">The static class Type to use as the log context.</param>
    /// <returns>An ILogger enriched with the SourceContext from the static class name.</returns>
    public static ILogger ForStaticClass(this ILogger logger, Type staticClass)
    {
        if (staticClass is null)
        {
            throw new ArgumentNullException(nameof(staticClass));
        }

        // Not strictly required, but you can guard if you only want truly static classes.
        // A 'static' class in C# is just sealed + abstract at runtime, so:
        if (!staticClass.IsAbstract || !staticClass.IsSealed)
        {
            throw new ArgumentException($"Type {staticClass.FullName} is not a static class.", nameof(staticClass));
        }

        return logger.ForContext(staticClass);
    }
}
