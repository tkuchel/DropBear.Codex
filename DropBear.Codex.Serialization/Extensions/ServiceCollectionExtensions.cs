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
using Microsoft.Extensions.Logging;
using Serilog;
using JsonSerializer = DropBear.Codex.Serialization.Serializers.JsonSerializer;

#endregion

namespace DropBear.Codex.Serialization.Extensions;

/// <summary>
///     Extension methods for registering serialization services with an IServiceCollection.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    private static readonly Microsoft.Extensions.Logging.ILogger Logger = CreateLogger();

    private static Microsoft.Extensions.Logging.ILogger CreateLogger()
    {
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Log.ForContext(typeof(ServiceCollectionExtensions)));
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        return loggerFactory.CreateLogger(nameof(ServiceCollectionExtensions));
    }

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

        LogAddingSerializationServices(Logger);

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

            LogSerializationServicesAdded(Logger);
        }
        catch (Exception ex)
        {
            LogSerializerBuildFailed(Logger, ex);
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

            LogAddingSerializationServicesWithResult(Logger);

            var builder = new SerializationBuilder();
            configure(builder);

            var serializer = builder.Build();
            services.AddSingleton(serializer);

            LogSerializationServicesAddedWithResult(Logger);
            return Result<IServiceCollection, SerializationError>.Success(services);
        }
        catch (Exception ex)
        {
            LogSerializerBuildFailedWithResult(Logger, ex);
            return Result<IServiceCollection, SerializationError>.Failure(
                new SerializationError($"Failed to configure serialization services: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Adds JSON streaming deserialization services to the specified IServiceCollection.
    ///     This enables element-by-element deserialization of large JSON arrays.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="options">Optional JSON serializer options for streaming deserialization.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddJsonStreamingDeserializer(this IServiceCollection services,
        JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        LogAddingJsonStreamingDeserializer(Logger);

        services.AddSingleton<IStreamingSerializer>(provider =>
        {
            var logger = provider.GetService<Serilog.ILogger>() ?? Log.Logger;
            return new Serializers.JsonStreamingDeserializer(options, logger);
        });

        LogJsonStreamingDeserializerAdded(Logger);
        return services;
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

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Adding serialization services.")]
    static partial void LogAddingSerializationServices(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Serialization services successfully added.")]
    static partial void LogSerializationServicesAdded(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to build and register the serializer.")]
    static partial void LogSerializerBuildFailed(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Adding serialization services with result type handling.")]
    static partial void LogAddingSerializationServicesWithResult(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Serialization services successfully added with result type handling.")]
    static partial void LogSerializationServicesAddedWithResult(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to build and register the serializer with result type handling.")]
    static partial void LogSerializerBuildFailedWithResult(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Adding JSON streaming deserializer services.")]
    static partial void LogAddingJsonStreamingDeserializer(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "JSON streaming deserializer services successfully added.")]
    static partial void LogJsonStreamingDeserializerAdded(Microsoft.Extensions.Logging.ILogger logger);

    #endregion
}
