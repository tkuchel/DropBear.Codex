#region

using System.Runtime.Versioning;
using System.Text.Json;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Factories;
using DropBear.Codex.Serialization.Interfaces;
using DropBear.Codex.Serialization.Writers;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using JsonSerializer = DropBear.Codex.Serialization.Serializers.JsonSerializer;

#endregion

namespace DropBear.Codex.Serialization.Extensions;

/// <summary>
///     Extension methods for registering serialization services with an IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(ServiceCollectionExtensions));

    /// <summary>
    ///     Adds serialization services to the specified IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configure">A delegate to configure the serialization builder.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if serializer configuration or building fails.</exception>
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddSerializationServices(this IServiceCollection services,
        Action<SerializationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        Logger.Information("Adding serialization services.");

        try
        {
            var builder = new SerializationBuilder();
            configure(builder);

            var serializer = builder.Build();
            services.AddSingleton(serializer);

            // Register additional service interfaces
            services.AddTransient<ISerializerReader>(provider =>
                new JsonSerializerReader(
                    provider.GetRequiredService<ISerializer>().GetJsonSerializerOptions()));

            services.AddTransient<ISerializerWriter>(provider =>
                new JsonSerializerWriter(
                    provider.GetRequiredService<ISerializer>().GetJsonSerializerOptions()));

            Logger.Information("Serialization services successfully added.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to build and register the serializer.");
            throw new InvalidOperationException("Failed to configure serialization services.", ex);
        }

        return services;
    }

    /// <summary>
    ///     Adds serialization services to the specified IServiceCollection with result type error handling.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configure">A delegate to configure the serialization builder.</param>
    /// <returns>A Result indicating success or failure of the registration.</returns>
    [SupportedOSPlatform("windows")]
    public static Result<IServiceCollection, SerializationError> AddSerializationServicesWithResult(
        this IServiceCollection services, Action<SerializationBuilder> configure)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(services, nameof(services));
            ArgumentNullException.ThrowIfNull(configure, nameof(configure));

            Logger.Information("Adding serialization services with result type handling.");

            var builder = new SerializationBuilder();
            configure(builder);

            var serializer = builder.Build();
            services.AddSingleton(serializer);

            Logger.Information("Serialization services successfully added with result type handling.");
            return Result<IServiceCollection, SerializationError>.Success(services);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to build and register the serializer with result type handling.");
            return Result<IServiceCollection, SerializationError>.Failure(
                new SerializationError($"Failed to configure serialization services: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Gets the JsonSerializerOptions from the ISerializer, if available.
    /// </summary>
    /// <param name="serializer">The serializer to get options from.</param>
    /// <returns>The JsonSerializerOptions or default options if not available.</returns>
    private static JsonSerializerOptions GetJsonSerializerOptions(this ISerializer serializer)
    {
        // This is a utility method to extract options from the serializer if possible
        if (serializer is JsonSerializer jsonSerializer)
        {
            return jsonSerializer.Options;
        }

        // Provide default options if not available
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
