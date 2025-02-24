#region

using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace DropBear.Codex.Core.Extensions;

/// <summary>
///     Extension methods for configuring MessagePack serialization in the dependency injection container.
/// </summary>
public static class MessagePackServiceCollectionExtensions
{
    /// <summary>
    ///     Adds MessagePack serialization configuration to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
    public static IServiceCollection AddMessagePackSerialization(
        this IServiceCollection services,
        Action<MessagePackSerializerOptions>? configureOptions = null)
    {
        var options = MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4BlockArray)
            .WithResolver(StandardResolverAllowPrivate.Instance)
            .WithSecurity(MessagePackSecurity.UntrustedData);

        configureOptions?.Invoke(options);

        MessagePackSerializer.DefaultOptions = options;

        services.AddSingleton(options);
        return services;
    }
}
