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
using Microsoft.Extensions.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Extensions;

/// <summary>
///     Extension methods for SerializerFactory and SerializationBuilder.
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class SerializerFactoryExtensions
{
    private static readonly Microsoft.Extensions.Logging.ILogger Logger = CreateLogger();

    private static Microsoft.Extensions.Logging.ILogger CreateLogger()
    {
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Log.ForContext(typeof(SerializerFactoryExtensions)));
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        return loggerFactory.CreateLogger(nameof(SerializerFactoryExtensions));
    }

    /// <summary>
    ///     Configures the builder with optimized JSON serialization options.
    /// </summary>
    /// <param name="builder">The serialization builder to configure.</param>
    /// <param name="writeIndented">Whether to write indented JSON (pretty-print).</param>
    /// <returns>The configured serialization builder for chaining.</returns>
    public static SerializationBuilder WithDefaultJsonOptions(this SerializationBuilder builder,
        bool writeIndented = false)
    {
        LogConfiguringJsonSerialization(Logger, writeIndented);

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
        LogConfiguringMessagePackSerialization(Logger, resolverEnabled);

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
            LogSelectingCompressionProvider(Logger, providerType.Name);

            if (!typeof(ICompressionProvider).IsAssignableFrom(providerType))
            {
                var errorMessage = $"Selected type {providerType.Name} does not implement ICompressionProvider.";
                LogCompressionProviderError(Logger, errorMessage);
                throw new ArgumentException(errorMessage, nameof(providerTypeSelector));
            }

            return builder.WithCompression(providerType);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            var errorMessage = "Failed to select compression provider.";
            LogCompressionProviderSelectionFailed(Logger, ex, errorMessage);
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
            LogSelectingEncryptionProvider(Logger, providerType.Name);

            if (!typeof(IEncryptionProvider).IsAssignableFrom(providerType))
            {
                var errorMessage = $"Selected type {providerType.Name} does not implement IEncryptionProvider.";
                LogEncryptionProviderError(Logger, errorMessage);
                throw new ArgumentException(errorMessage, nameof(providerTypeSelector));
            }

            return builder.WithEncryption(providerType);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            var errorMessage = "Failed to select encryption provider.";
            LogEncryptionProviderSelectionFailed(Logger, ex, errorMessage);
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
        LogValidatingConfiguration(Logger);

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
                LogNoProvidersConfigured(Logger);
            }

            // Log validation success
            LogConfigurationValidated(Logger);
            return Result<bool, SerializationError>.Success(true);
        }
        catch (Exception ex)
        {
            var error = new SerializationError($"Configuration validation failed: {ex.Message}");
            LogConfigurationValidationError(Logger, ex);
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

        LogRegisteringSerializationServices(Logger);

        var builder = new SerializationBuilder();
        configure(builder);

        try
        {
            var serializer = builder.Build();
            services.AddSingleton(serializer);

            // Register all dependency services
            services.AddSingleton<IStreamSerializer, JsonStreamSerializer>();

            LogSerializationServicesRegistered(Logger);
            return services;
        }
        catch (Exception ex)
        {
            LogSerializationServicesRegistrationFailed(Logger, ex);
            throw new InvalidOperationException("Failed to register serialization services.", ex);
        }
    }

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Configuring JSON serialization with WriteIndented: {WriteIndented}")]
    static partial void LogConfiguringJsonSerialization(Microsoft.Extensions.Logging.ILogger logger, bool writeIndented);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Configuring MessagePack serialization with ResolverEnabled: {ResolverEnabled}")]
    static partial void LogConfiguringMessagePackSerialization(Microsoft.Extensions.Logging.ILogger logger, bool resolverEnabled);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Selecting compression provider type: {ProviderType}")]
    static partial void LogSelectingCompressionProvider(Microsoft.Extensions.Logging.ILogger logger, string providerType);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{ErrorMessage}")]
    static partial void LogCompressionProviderError(Microsoft.Extensions.Logging.ILogger logger, string errorMessage);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{ErrorMessage}")]
    static partial void LogCompressionProviderSelectionFailed(Microsoft.Extensions.Logging.ILogger logger, Exception ex, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Selecting encryption provider type: {ProviderType}")]
    static partial void LogSelectingEncryptionProvider(Microsoft.Extensions.Logging.ILogger logger, string providerType);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{ErrorMessage}")]
    static partial void LogEncryptionProviderError(Microsoft.Extensions.Logging.ILogger logger, string errorMessage);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{ErrorMessage}")]
    static partial void LogEncryptionProviderSelectionFailed(Microsoft.Extensions.Logging.ILogger logger, Exception ex, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Validating serialization configuration.")]
    static partial void LogValidatingConfiguration(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No providers (compression, encryption, or encoding) have been configured.")]
    static partial void LogNoProvidersConfigured(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Serialization configuration validated successfully.")]
    static partial void LogConfigurationValidated(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error validating serialization configuration.")]
    static partial void LogConfigurationValidationError(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Registering serialization services.")]
    static partial void LogRegisteringSerializationServices(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Serialization services registered successfully.")]
    static partial void LogSerializationServicesRegistered(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to build and register serialization services.")]
    static partial void LogSerializationServicesRegistrationFailed(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    #endregion
}
