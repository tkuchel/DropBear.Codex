#region

using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

#endregion

namespace DropBear.Codex.Core.Extensions;

/// <summary>
///     Extension methods for configuring MessagePack serialization in the DI container.
///     Optimized for .NET 9 with modern patterns.
/// </summary>
public static class MessagePackServiceCollectionExtensions
{
    /// <summary>
    ///     Adds MessagePack serialization with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessagePackSerialization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = MessagePackConfig.GetOptions();

        services.TryAddSingleton(options);
        return services;
    }

    /// <summary>
    ///     Adds MessagePack serialization with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Delegate that returns the configured options instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessagePackSerialization(
        this IServiceCollection services,
        Func<MessagePackSerializerOptions, MessagePackSerializerOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = configureOptions(MessagePackConfig.GetOptions())
                      ?? throw new InvalidOperationException("configureOptions delegate returned null.");

        services.TryAddSingleton(options);

        return services;
    }

    /// <summary>
    ///     Adds MessagePack serialization using a builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureBuilder">Action to configure the builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessagePackSerialization(
        this IServiceCollection services,
        Action<MessagePackConfigBuilder> configureBuilder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureBuilder);

        var builder = MessagePackConfig.CreateBuilder();
        configureBuilder(builder);
        var options = builder.Build();

        services.TryAddSingleton(options);

        return services;
    }

    /// <summary>
    ///     Adds high-performance MessagePack serialization (no compression).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHighPerformanceMessagePackSerialization(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = MessagePackConfig.GetHighPerformanceOptions();

        services.TryAddSingleton(options);
        return services;
    }

    /// <summary>
    ///     Adds compact MessagePack serialization (maximum compression).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCompactMessagePackSerialization(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = MessagePackConfig.GetCompactOptions();

        services.TryAddSingleton(options);
        return services;
    }
}
