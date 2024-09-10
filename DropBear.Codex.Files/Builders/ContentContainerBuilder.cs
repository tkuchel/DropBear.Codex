#region

using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Enums;
using DropBear.Codex.Files.Models;
using DropBear.Codex.Serialization.Factories;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Builders;

/// <summary>
///     Builder class for creating instances of <see cref="ContentContainer" /> with various properties and content.
/// </summary>
[SupportedOSPlatform("windows")]
public class ContentContainerBuilder
{
    private readonly ContentContainer _container = new();
    private readonly ILogger _logger;

    private Type? _compressionProviderType;
    private Type? _encryptionProviderType;
    private Type? _serializerType;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentContainerBuilder" /> class.
    /// </summary>
    public ContentContainerBuilder()
    {
        _logger = LoggerFactory.Logger.ForContext<ContentContainerBuilder>();
    }

    /// <summary>
    ///     Sets the data for the ContentContainer.
    /// </summary>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <param name="data">The data to set.</param>
    /// <returns>The current <see cref="ContentContainerBuilder" /> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    public ContentContainerBuilder WithData<T>(T data)
    {
        try
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
        catch (Exception ex)
        {
            _logger.Error(ex, "Error setting data");
            throw;
        }
    }

    /// <summary>
    ///     Enables a flag on the ContentContainer.
    /// </summary>
    /// <param name="flag">The flag to enable.</param>
    /// <returns>The current <see cref="ContentContainerBuilder" /> instance.</returns>
    public ContentContainerBuilder WithFlag(ContentContainerFlags flag)
    {
        try
        {
            _container.EnableFlag(flag);
            _logger.Debug("Enabled flag: {Flag}", flag);
            return this;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error enabling flag");
            throw;
        }
    }

    /// <summary>
    ///     Sets the content type for the ContentContainer.
    /// </summary>
    /// <param name="contentType">The content type to set.</param>
    /// <returns>The current <see cref="ContentContainerBuilder" /> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when contentType is null or empty.</exception>
    public ContentContainerBuilder WithContentType(string contentType)
    {
        try
        {
            if (string.IsNullOrEmpty(contentType))
            {
                throw new ArgumentException("Content type cannot be null or empty.", nameof(contentType));
            }

            _container.SetContentType(contentType);
            _logger.Debug("Set content type: {ContentType}", contentType);
            return this;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error setting content type");
            throw;
        }
    }

    /// <summary>
    ///     Builds and returns the configured ContentContainer instance asynchronously.
    /// </summary>
    /// <returns>The built <see cref="ContentContainer" /> instance.</returns>
    public async Task<ContentContainer> BuildAsync()
    {
        try
        {
            // Add provider configurations
            if (_compressionProviderType != null)
            {
                _container.AddProvider("CompressionProvider", _compressionProviderType);
                _logger.Debug("Added CompressionProvider: {Provider}", _compressionProviderType.Name);
            }

            if (_encryptionProviderType != null)
            {
                _container.AddProvider("EncryptionProvider", _encryptionProviderType);
                _logger.Debug("Added EncryptionProvider: {Provider}", _encryptionProviderType.Name);
            }

            if (_serializerType != null)
            {
                _container.AddProvider("Serializer", _serializerType);
                _logger.Debug("Added Serializer: {Provider}", _serializerType.Name);
            }

            // Handle serialization
            if (_serializerType != null && _container.RequiresSerialization())
            {
                var serializerBuilder = new SerializationBuilder();
                _container.ConfigureContainerSerializer(serializerBuilder);
                var serializer = serializerBuilder.Build();
                var data = _container.TemporaryData;

                if (data == null)
                {
                    throw new InvalidOperationException("No data available for serialization.");
                }

                // Serialize the data
                var serializedData = await serializer.SerializeAsync(data).ConfigureAwait(false);
                if (serializedData == null || serializedData.Length == 0)
                {
                    throw new InvalidOperationException("Serialization failed to produce valid data.");
                }

                _container.Data = serializedData;
                _logger.Debug("Serialized data of type {DataType} for ContentContainer.", data.GetType().Name);
            }
            else
            {
                // Fallback: Convert TemporaryData to a byte array if no explicit serialization is required
                var data = _container.TemporaryData;
                if (data != null)
                {
                    // Use default or fallback serialization (e.g., JSON or Binary)
                    _container.Data = ConvertToByteArray(data);
                    _logger.Debug(
                        "Using fallback serialization for TemporaryData directly as Data for ContentContainer.");
                }
                else
                {
                    throw new InvalidOperationException("TemporaryData is null and no serialization was required.");
                }
            }

            // Compute the hash after serialization or setting data
            if (_container.Data != null)
            {
                _container.ComputeAndSetHash();
                _logger.Debug("Computed hash for ContentContainer.");
            }
            else
            {
                throw new InvalidOperationException("ContentContainer has no data to compute hash.");
            }

            _logger.Information("Built ContentContainer successfully.");
            return _container;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error building ContentContainer");
            throw;
        }
    }


    #region Provider Support (Compression, Encryption, Serialization)

    /// <summary>
    ///     Configures the container to use the specified serializer.
    /// </summary>
    /// <typeparam name="T">The type of the serializer.</typeparam>
    /// <returns>The current <see cref="ContentContainerBuilder" /> instance.</returns>
    public ContentContainerBuilder WithSerializer<T>() where T : ISerializer
    {
        _serializerType = typeof(T);
        _logger.Debug("Configured serializer: {SerializerType}", _serializerType.Name);
        return this;
    }

    /// <summary>
    ///     Configures the container to use the specified compression provider.
    /// </summary>
    /// <typeparam name="T">The type of the compression provider.</typeparam>
    /// <returns>The current <see cref="ContentContainerBuilder" /> instance.</returns>
    public ContentContainerBuilder WithCompression<T>() where T : ICompressionProvider
    {
        _compressionProviderType = typeof(T);
        _logger.Debug("Configured compression provider: {CompressionProviderType}", _compressionProviderType.Name);
        return this;
    }

    /// <summary>
    ///     Configures the container to use the specified encryption provider.
    /// </summary>
    /// <typeparam name="T">The type of the encryption provider.</typeparam>
    /// <returns>The current <see cref="ContentContainerBuilder" /> instance.</returns>
    public ContentContainerBuilder WithEncryption<T>() where T : IEncryptionProvider
    {
        _encryptionProviderType = typeof(T);
        _logger.Debug("Configured encryption provider: {EncryptionProviderType}", _encryptionProviderType.Name);
        return this;
    }

    /// <summary>
    ///     Skips serialization for this content container.
    /// </summary>
    /// <returns>The current <see cref="ContentContainerBuilder" /> instance.</returns>
    public ContentContainerBuilder NoSerialization()
    {
        _serializerType = null;
        _logger.Debug("Disabled serialization.");
        return this;
    }

    private byte[] ConvertToByteArray(object data)
    {
        // Example: using JSON serialization as a fallback
        var jsonString = JsonSerializer.Serialize(data);
        return Encoding.UTF8.GetBytes(jsonString);

        // Alternatively, you could use binary serialization, or other methods depending on the object type.
    }

    #endregion
}
