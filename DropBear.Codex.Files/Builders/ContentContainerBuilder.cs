#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Files.Enums;
using DropBear.Codex.Files.Errors;
using DropBear.Codex.Files.Models;
using DropBear.Codex.Serialization.Factories;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Builders;

/// <summary>
///     A builder class to configure and construct a <see cref="ContentContainer" /> object.
///     Supports serialization, compression, and encryption settings.
/// </summary>
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

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentContainerBuilder" /> class.
    /// </summary>
    public ContentContainerBuilder()
    {
        _logger = LoggerFactory.Logger.ForContext<ContentContainerBuilder>();
    }

    /// <summary>
    ///     Sets the data to be stored in the <see cref="ContentContainer" />.
    /// </summary>
    /// <typeparam name="T">The type of the data being stored.</typeparam>
    /// <param name="data">The data object.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="data" /> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if setting data on the container fails.</exception>
    public ContentContainerBuilder WithData<T>(T data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "Data cannot be null.");
        }

        var result = _container.SetData(data);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to set data: {result.Error?.Message}");
        }

        _logger.Debug("Set data of type {DataType}", typeof(T).Name);
        return this;
    }

    /// <summary>
    ///     Enables a specific <see cref="ContentContainerFlags" /> on the container.
    /// </summary>
    /// <param name="flag">The flag to enable.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ContentContainerBuilder WithFlag(ContentContainerFlags flag)
    {
        _container.EnableFlag(flag);
        _logger.Debug("Enabled flag: {Flag}", flag);
        return this;
    }

    /// <summary>
    ///     Sets the content type identifier on the <see cref="ContentContainer" />.
    /// </summary>
    /// <param name="contentType">A string representing the content type.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contentType" /> is null or empty.</exception>
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

    /// <summary>
    ///     Configures which <see cref="ISerializer" /> implementation to use.
    /// </summary>
    /// <typeparam name="T">A class implementing <see cref="ISerializer" />.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    public ContentContainerBuilder WithSerializer<T>() where T : ISerializer
    {
        _serializerType = typeof(T);
        _logger.Debug("Configured serializer: {SerializerType}", _serializerType.Name);
        return this;
    }

    /// <summary>
    ///     Configures which <see cref="ICompressionProvider" /> implementation to use.
    /// </summary>
    /// <typeparam name="T">A class implementing <see cref="ICompressionProvider" />.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    public ContentContainerBuilder WithCompression<T>() where T : ICompressionProvider
    {
        _compressionProviderType = typeof(T);
        _logger.Debug("Configured compression provider: {CompressionProviderType}", _compressionProviderType.Name);
        return this;
    }

    /// <summary>
    ///     Configures which <see cref="IEncryptionProvider" /> implementation to use.
    /// </summary>
    /// <typeparam name="T">A class implementing <see cref="IEncryptionProvider" />.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    public ContentContainerBuilder WithEncryption<T>() where T : IEncryptionProvider
    {
        _encryptionProviderType = typeof(T);
        _logger.Debug("Configured encryption provider: {EncryptionProviderType}", _encryptionProviderType.Name);
        return this;
    }

    /// <summary>
    ///     Assigns the public and private key paths for potential encryption usage.
    /// </summary>
    /// <param name="publicKeyPath">Path to the public key file.</param>
    /// <param name="privateKeyPath">Path to the private key file.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ContentContainerBuilder WithKeys(string publicKeyPath, string privateKeyPath)
    {
        _publicKeyPath = publicKeyPath;
        _privateKeyPath = privateKeyPath;

        _logger.Debug("Configured keys: Public ({PublicKeyPath}), Private ({PrivateKeyPath})",
            _publicKeyPath, _privateKeyPath);
        return this;
    }

    /// <summary>
    ///     Builds and configures the <see cref="ContentContainer" /> asynchronously.
    ///     This includes serializing data, if a serializer is specified.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A <see cref="Result{ContentContainer, BuilderError}" /> containing the fully configured
    ///     <see cref="ContentContainer" />
    ///     or an error.
    /// </returns>
    public async Task<Result<ContentContainer, BuilderError>> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Assign providers to the container if specified
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

            // Build the container's serializer via a builder
            var serializerBuilder = new SerializationBuilder();
            _container.ConfigureContainerSerializer(serializerBuilder);

            // If keys are provided, attach them to the serializer builder
            if (_publicKeyPath != null && _privateKeyPath != null)
            {
                serializerBuilder.WithKeys(_publicKeyPath, _privateKeyPath);
                _logger.Debug("Added encryption keys to serializer builder.");
            }

            // Use cancellation token to check for cancellation
            cancellationToken.ThrowIfCancellationRequested();

            var serializer = _serializerType != null ? serializerBuilder.Build() : null;

            // Perform serialization if required
            if (_serializerType != null && _container.RequiresSerialization())
            {
                var data = _container.TemporaryData;
                if (data == null)
                {
                    return Result<ContentContainer, BuilderError>.Failure(
                        BuilderError.BuildFailed("No data available for serialization."));
                }

                if (serializer != null && serializer.IsSuccess)
                {
                    var serializedData = await serializer.Value!.SerializeAsync(data, cancellationToken)
                        .ConfigureAwait(false);

                    if (serializedData.IsSuccess)
                    {
                        _container.Data = serializedData.Value;
                    }
                    else
                    {
                        return Result<ContentContainer, BuilderError>.Failure(
                            BuilderError.BuildFailed("Failed to serialize data."));
                    }
                }
                else
                {
                    return Result<ContentContainer, BuilderError>.Failure(
                        BuilderError.BuildFailed("No serializer available for serialization."));
                }

                _logger.Debug("Serialized data for ContentContainer.");
            }

            // Finalize the container by computing hashes, etc.
            _container.ComputeAndSetHash();
            _logger.Information("Built ContentContainer successfully.");
            return Result<ContentContainer, BuilderError>.Success(_container);
        }
        catch (OperationCanceledException)
        {
            _logger.Information("ContentContainer build was canceled.");
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error building ContentContainer");
            return Result<ContentContainer, BuilderError>.Failure(
                BuilderError.BuildFailed(ex), ex);
        }
    }
}
