#region

using System.Runtime.Versioning;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Interfaces;
using DropBear.Codex.Serialization.Providers;
using DropBear.Codex.Serialization.Serializers;
using MessagePack;
using MessagePack.ImmutableCollection;
using MessagePack.Resolvers;
using Serilog;
using JsonSerializer = System.Text.Json.JsonSerializer;
using MessagePackSerializer = MessagePack.MessagePackSerializer;

#endregion

namespace DropBear.Codex.Serialization.Factories;

/// <summary>
///     Builder class for configuring serialization settings.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SerializationBuilder
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<SerializationBuilder>();
    private readonly SerializationConfig _config;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SerializationBuilder" /> class.
    /// </summary>
    public SerializationBuilder()
    {
        _config = new SerializationConfig();
        Logger.Information("SerializationBuilder initialized.");
    }

    public SerializationBuilder WithSerializer<T>() where T : ISerializer
    {
        Logger.Information($"Configuring serializer type: {typeof(T).Name}");
        _config.SerializerType = typeof(T);
        return this;
    }

    public SerializationBuilder WithSerializer(Type serializerType)
    {
        _ = serializerType ??
            throw new ArgumentNullException(nameof(serializerType), "Serializer type cannot be null.");
        if (!typeof(ISerializer).IsAssignableFrom(serializerType))
        {
            var message = "The type must implement the ISerializer interface.";
            Logger.Error(message);
            throw new ArgumentException(message, nameof(serializerType));
        }

        Logger.Information($"Configuring serializer type: {serializerType.Name}");
        _config.SerializerType = serializerType;
        return this;
    }

    public SerializationBuilder WithCompression(Type compressionType)
    {
        ValidateProviderType<ICompressionProvider>(compressionType, nameof(compressionType));
        Logger.Information($"Configuring compression provider: {compressionType.Name}");
        _config.CompressionProviderType = compressionType;
        return this;
    }

    public SerializationBuilder WithEncoding(Type encodingType)
    {
        ValidateProviderType<IEncodingProvider>(encodingType, nameof(encodingType));
        Logger.Information($"Configuring encoding provider: {encodingType.Name}");
        _config.EncodingProviderType = encodingType;
        return this;
    }

    public SerializationBuilder WithEncryption(Type encryptionType)
    {
        ValidateProviderType<IEncryptionProvider>(encryptionType, nameof(encryptionType));
        Logger.Information($"Configuring encryption provider: {encryptionType.Name}");
        _config.EncryptionProviderType = encryptionType;
        return this;
    }

    public SerializationBuilder WithEncryption<T>() where T : IEncryptionProvider
    {
        Logger.Information($"Configuring encryption provider: {typeof(T).Name}");
        _config.EncryptionProviderType = typeof(T);
        return this;
    }

    public SerializationBuilder WithStreamSerializer<T>() where T : IStreamSerializer
    {
        Logger.Information($"Configuring stream serializer: {typeof(T).Name}");
        _config.StreamSerializerType = typeof(T);
        return this;
    }

    public SerializationBuilder WithCompression<T>() where T : ICompressionProvider
    {
        Logger.Information($"Configuring compression provider: {typeof(T).Name}");
        _config.CompressionProviderType = typeof(T);
        return this;
    }

    public SerializationBuilder WithEncoding<T>() where T : IEncodingProvider
    {
        Logger.Information($"Configuring encoding provider: {typeof(T).Name}");
        _config.EncodingProviderType = typeof(T);
        return this;
    }

    public SerializationBuilder WithKeys(string publicKeyPath, string privateKeyPath)
    {
        ValidateFileExists(publicKeyPath, "Public key file");
        ValidateFileExists(privateKeyPath, "Private key file");

        Logger.Information("Configuring key paths for encryption.");
        _config.PublicKeyPath = publicKeyPath;
        _config.PrivateKeyPath = privateKeyPath;

        return this;
    }

    public SerializationBuilder WithJsonSerializerOptions(JsonSerializerOptions options)
    {
        _config.SerializerType ??= typeof(Serializers.JsonSerializer);
        _config.JsonSerializerOptions = options;
        Logger.Information("Configured JSON serializer options.");
        return this;
    }

    public SerializationBuilder WithDefaultJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReferenceHandler = ReferenceHandler.Preserve,
            MaxDepth = 64,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
            Converters = { new JsonStringEnumConverter() }
        };

        _config.SerializerType ??= typeof(Serializers.JsonSerializer);
        _config.JsonSerializerOptions = options;
        Logger.Information("Configured default JSON serializer options.");
        return this;
    }

    public SerializationBuilder WithMessagePackSerializerOptions(MessagePackSerializerOptions options)
    {
        _config.MessagePackSerializerOptions = options;
        Logger.Information("Configured MessagePack serializer options.");
        return this;
    }

    public SerializationBuilder WithDefaultMessagePackSerializerOptions()
    {
        var options = MessagePackSerializerOptions.Standard
            .WithResolver(CompositeResolver.Create(
                ImmutableCollectionResolver.Instance,
                StandardResolverAllowPrivate.Instance,
                StandardResolver.Instance
            ))
            .WithSecurity(MessagePackSecurity.UntrustedData);

        _config.MessagePackSerializerOptions = options;
        Logger.Information("Configured default MessagePack serializer options.");
        return this;
    }

    public SerializationBuilder WithDefaultConfiguration()
    {
        Logger.Information("Configuring default serialization settings.");
        return WithJsonSerializerOptions(new JsonSerializerOptions { WriteIndented = true })
            .WithCompression<GZipCompressionProvider>();
    }

    public ISerializer Build()
    {
        Logger.Information("Building the serializer.");
        _config.SerializerType = _config.SerializerType switch
        {
            null when _config.StreamSerializerType is null => throw new InvalidOperationException(
                "No serializer type or stream serializer specified. Please specify one before building."),
            null when _config.StreamSerializerType is not null => typeof(StreamSerializerAdapter),
            _ => _config.SerializerType
        };

        ValidateSerializerConfigurations();

        Logger.Information($"Serializer built successfully with type: {_config.SerializerType?.Name}");
        return SerializerFactory.CreateSerializer(_config);
    }

    private void ValidateProviderType<T>(Type providerType, string paramName)
    {
        _ = providerType ?? throw new ArgumentNullException(paramName, $"{paramName} cannot be null.");
        if (!typeof(T).IsAssignableFrom(providerType))
        {
            var message = $"The type must implement the {typeof(T).Name} interface.";
            Logger.Error(message);
            throw new ArgumentException(message, paramName);
        }
    }

    private void ValidateFileExists(string path, string fileType)
    {
        if (!File.Exists(path))
        {
            var message = $"{fileType} not found at path: {path}";
            Logger.Error(message);
            throw new FileNotFoundException(message);
        }
    }

    private void ValidateSerializerConfigurations()
    {
        if (_config.SerializerType == typeof(JsonSerializer) && _config.JsonSerializerOptions is null)
        {
            var message = "JsonSerializerOptions must be specified for JsonSerializer.";
            Logger.Error(message);
            throw new InvalidOperationException(message);
        }

        if (_config.SerializerType == typeof(MessagePackSerializer) && _config.MessagePackSerializerOptions is null)
        {
            var message = "MessagePackSerializerOptions must be specified for MessagePackSerializer.";
            Logger.Error(message);
            throw new InvalidOperationException(message);
        }
    }
}
