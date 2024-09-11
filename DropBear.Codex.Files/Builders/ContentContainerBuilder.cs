#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Enums;
using DropBear.Codex.Files.Models;
using DropBear.Codex.Serialization.Factories;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Builders;

[SupportedOSPlatform("windows")]
public class ContentContainerBuilder
{
    private readonly ContentContainer _container = new();
    private readonly ILogger _logger;
    private Type? _compressionProviderType;
    private Type? _encryptionProviderType;
    private string? _privateKeyPath;
    private string? _publicKeyPath;
    private Type? _serializerType;

    public ContentContainerBuilder()
    {
        _logger = LoggerFactory.Logger.ForContext<ContentContainerBuilder>();
    }

    public ContentContainerBuilder WithData<T>(T data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "Data cannot be null.");
        }

        var result = _container.SetData(data);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to set data: {result.ErrorMessage}");
        }

        _logger.Debug("Set data of type {DataType}", typeof(T).Name);
        return this;
    }

    public ContentContainerBuilder WithFlag(ContentContainerFlags flag)
    {
        _container.EnableFlag(flag);
        _logger.Debug("Enabled flag: {Flag}", flag);
        return this;
    }

    public ContentContainerBuilder WithContentType(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            throw new ArgumentException("Content type cannot be null or empty.", nameof(contentType));
        }

        _container.SetContentType(contentType);
        _logger.Debug("Set content type: {ContentType}", contentType);
        return this;
    }

    public ContentContainerBuilder WithSerializer<T>() where T : ISerializer
    {
        _serializerType = typeof(T);
        _logger.Debug("Configured serializer: {SerializerType}", _serializerType.Name);
        return this;
    }

    public ContentContainerBuilder WithCompression<T>() where T : ICompressionProvider
    {
        _compressionProviderType = typeof(T);
        _logger.Debug("Configured compression provider: {CompressionProviderType}", _compressionProviderType.Name);
        return this;
    }

    public ContentContainerBuilder WithEncryption<T>() where T : IEncryptionProvider
    {
        _encryptionProviderType = typeof(T);
        _logger.Debug("Configured encryption provider: {EncryptionProviderType}", _encryptionProviderType.Name);
        return this;
    }

    public ContentContainerBuilder WithKeys(string publicKeyPath, string privateKeyPath)
    {
        _publicKeyPath = publicKeyPath;
        _privateKeyPath = privateKeyPath;

        _logger.Debug("Configured keys: Public ({PublicKeyPath}), Private ({PrivateKeyPath})", _publicKeyPath,
            _privateKeyPath);
        return this;
    }

    public async Task<ContentContainer> BuildAsync()
    {
        try
        {
            if (_serializerType != null)
            {
                _container.AddProvider("Serializer", _serializerType);
            }

            if (_compressionProviderType != null)
            {
                _container.AddProvider("CompressionProvider", _compressionProviderType);
            }

            if (_encryptionProviderType != null)
            {
                _container.AddProvider("EncryptionProvider", _encryptionProviderType);
            }

            // Build the SerializationBuilder and configure it
            var serializerBuilder = new SerializationBuilder();
            _container.ConfigureContainerSerializer(serializerBuilder);

            if (_publicKeyPath != null && _privateKeyPath != null)
            {
                serializerBuilder.WithKeys(_publicKeyPath, _privateKeyPath);
                _logger.Debug("Added encryption keys to serializer builder.");
            }

            var serializer = _serializerType != null ? serializerBuilder.Build() : null;

            // Handle serialization if required
            if (_serializerType != null && _container.RequiresSerialization())
            {
                var data = _container.TemporaryData;
                if (data == null)
                {
                    throw new InvalidOperationException("No data available for serialization.");
                }

                var serializedData = await serializer.SerializeAsync(data).ConfigureAwait(false);
                _container.Data = serializedData;
                _logger.Debug("Serialized data for ContentContainer.");
            }

            _container.ComputeAndSetHash();
            _logger.Information("Built ContentContainer successfully.");
            return _container;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error building ContentContainer");
            throw;
        }
    }
}
