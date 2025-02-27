#region

using System.Runtime.Versioning;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Factories;
using DropBear.Codex.Serialization.Interfaces;
using DropBear.Codex.Serialization.Providers;
using DropBear.Codex.Serialization.Serializers;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Extensions;

/// <summary>
///     Extension methods for SerializerFactory and SerializationBuilder.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SerializerFactoryExtensions
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(SerializerFactoryExtensions));

    /// <summary>
    ///     Configures the builder with optimized JSON serialization options.
    /// </summary>
    /// <param name="builder">The serialization builder to configure.</param>
    /// <param name="writeIndented">Whether to write indented JSON (pretty-print).</param>
    /// <returns>The configured serialization builder for chaining.</returns>
    public static SerializationBuilder WithDefaultJsonOptions(this SerializationBuilder builder,
        bool writeIndented = false)
    {
        Logger.Information("Configuring JSON serialization with WriteIndented: {WriteIndented}", writeIndented);

        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReferenceHandler = ReferenceHandler.Preserve,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            MaxDepth = 64
        };

        return builder.WithJsonSerializerOptions(options);
    }

    /// <summary>
    ///     Configures the builder with optimized MessagePack serialization options.
    /// </summary>
    /// <param name="builder">The serialization builder to configure.</param>
    /// <param name="resolverEnabled">Whether to enable the standard resolver.</param>
    /// <returns>The configured serialization builder for chaining.</returns>
    public static SerializationBuilder WithDefaultMessagePackOptions(this SerializationBuilder builder,
        bool resolverEnabled = true)
    {
        Logger.Information("Configuring MessagePack serialization with ResolverEnabled: {ResolverEnabled}",
            resolverEnabled);

        var options = MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.UntrustedData)
            .WithCompression(MessagePackCompression.Lz4BlockArray);

        if (resolverEnabled)
        {
            options = options.WithResolver(CompositeResolver.Create(
                StandardResolverAllowPrivate.Instance,
                StandardResolver.Instance));
        }

        return builder.WithMessagePackSerializerOptions(options);
    }

    /// <summary>
    ///     Configures the builder with a dynamically selected compression provider.
    /// </summary>
    /// <param name="builder">The serialization builder to configure.</param>
    /// <param name="providerTypeSelector">A function that selects the compression provider type.</param>
    /// <returns>The configured serialization builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the selected type does not implement ICompressionProvider.</exception>
    public static SerializationBuilder WithDynamicCompression(this SerializationBuilder builder,
        Func<Type> providerTypeSelector)
    {
        ArgumentNullException.ThrowIfNull(providerTypeSelector, nameof(providerTypeSelector));

        try
        {
            var providerType = providerTypeSelector();
            Logger.Information("Selecting compression provider type: {ProviderType}", providerType.Name);

            if (!typeof(ICompressionProvider).IsAssignableFrom(providerType))
            {
                var errorMessage = $"Selected type {providerType.Name} does not implement ICompressionProvider.";
                Logger.Error(errorMessage);
                throw new ArgumentException(errorMessage, nameof(providerTypeSelector));
            }

            return builder.WithCompression(providerType);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            var errorMessage = "Failed to select compression provider.";
            Logger.Error(ex, errorMessage);
            throw new InvalidOperationException(errorMessage, ex);
        }
    }

    /// <summary>
    ///     Configures the builder with a dynamically selected encryption provider.
    /// </summary>
    /// <param name="builder">The serialization builder to configure.</param>
    /// <param name="providerTypeSelector">A function that selects the encryption provider type.</param>
    /// <returns>The configured serialization builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the selected type does not implement IEncryptionProvider.</exception>
    public static SerializationBuilder WithDynamicEncryption(this SerializationBuilder builder,
        Func<Type> providerTypeSelector)
    {
        ArgumentNullException.ThrowIfNull(providerTypeSelector, nameof(providerTypeSelector));

        try
        {
            var providerType = providerTypeSelector();
            Logger.Information("Selecting encryption provider type: {ProviderType}", providerType.Name);

            if (!typeof(IEncryptionProvider).IsAssignableFrom(providerType))
            {
                var errorMessage = $"Selected type {providerType.Name} does not implement IEncryptionProvider.";
                Logger.Error(errorMessage);
                throw new ArgumentException(errorMessage, nameof(providerTypeSelector));
            }

            return builder.WithEncryption(providerType);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            var errorMessage = "Failed to select encryption provider.";
            Logger.Error(ex, errorMessage);
            throw new InvalidOperationException(errorMessage, ex);
        }
    }

    /// <summary>
    ///     Configures the builder with both GZip compression and Base64 encoding.
    /// </summary>
    /// <param name="builder">The serialization builder to configure.</param>
    /// <returns>The configured serialization builder for chaining.</returns>
    public static SerializationBuilder WithDefaultCompression(this SerializationBuilder builder)
    {
        return builder
            .WithCompression<GZipCompressionProvider>()
            .WithEncoding<Base64EncodingProvider>();
    }

    /// <summary>
    ///     Validates the serialization configuration to ensure it is complete and consistent.
    /// </summary>
    /// <param name="config">The serialization configuration to validate.</param>
    /// <returns>True if the configuration is valid; otherwise, false.</returns>
    public static Result<bool, SerializationError> ValidateConfiguration(this SerializationConfig config)
    {
        Logger.Information("Validating serialization configuration.");

        try
        {
            // Check for essential components
            if (config.SerializerType is null)
            {
                return Result<bool, SerializationError>.Failure(
                    new SerializationError("SerializerType is required but was not specified."));
            }

            // Ensure that memory manager is configured
            if (config.RecyclableMemoryStreamManager is null)
            {
                return Result<bool, SerializationError>.Failure(
                    new SerializationError("RecyclableMemoryStreamManager is required but was not initialized."));
            }

            // Validate specific serializer configurations
            if (config.SerializerType.Name.Contains("Json", StringComparison.OrdinalIgnoreCase) &&
                config.JsonSerializerOptions is null)
            {
                return Result<bool, SerializationError>.Failure(
                    new SerializationError("JsonSerializerOptions must be provided when using a JSON serializer."));
            }

            if (config.SerializerType.Name.Contains("MessagePack", StringComparison.OrdinalIgnoreCase) &&
                config.MessagePackSerializerOptions is null)
            {
                return Result<bool, SerializationError>.Failure(
                    new SerializationError(
                        "MessagePackSerializerOptions must be provided when using a MessagePack serializer."));
            }

            // Validate combinations
            var hasValidProviders = config.CompressionProviderType is not null ||
                                    config.EncryptionProviderType is not null ||
                                    config.EncodingProviderType is not null;

            if (!hasValidProviders)
            {
                Logger.Warning("No providers (compression, encryption, or encoding) have been configured.");
            }

            // Log validation success
            Logger.Information("Serialization configuration validated successfully.");
            return Result<bool, SerializationError>.Success(true);
        }
        catch (Exception ex)
        {
            var error = new SerializationError($"Configuration validation failed: {ex.Message}");
            Logger.Error(ex, "Error validating serialization configuration.");
            return Result<bool, SerializationError>.Failure(error, ex);
        }
    }

    /// <summary>
    ///     Registers serialization services with the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to register the services with.</param>
    /// <param name="configure">An action to configure the serialization builder.</param>
    /// <returns>The service collection for chaining.</returns>
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddDropBearSerialization(this IServiceCollection services,
        Action<SerializationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        Logger.Information("Registering serialization services.");

        var builder = new SerializationBuilder();
        configure(builder);

        try
        {
            var serializer = builder.Build();
            services.AddSingleton(serializer);

            // Register all dependency services
            services.AddSingleton<IStreamSerializer, JsonStreamSerializer>();

            Logger.Information("Serialization services registered successfully.");
            return services;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to build and register serialization services.");
            throw new InvalidOperationException("Failed to register serialization services.", ex);
        }
    }
}
