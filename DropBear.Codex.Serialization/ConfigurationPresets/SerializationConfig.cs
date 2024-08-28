#region

using System.Text.Json;
using MessagePack;
using Microsoft.IO;

#endregion

namespace DropBear.Codex.Serialization.ConfigurationPresets;

/// <summary>
///     Represents the configuration settings for serialization.
/// </summary>
public class SerializationConfig
{
    /// <summary>
    ///     Gets or sets the type of serializer to be used.
    /// </summary>
    public Type? SerializerType { get; set; }

    /// <summary>
    ///     Gets or sets the type of compression provider for serialization.
    /// </summary>
    public Type? CompressionProviderType { get; set; }

    /// <summary>
    ///     Gets or sets the type of encoding provider for serialization.
    /// </summary>
    public Type? EncodingProviderType { get; set; }

    /// <summary>
    ///     Gets or sets the type of encryption provider for serialization.
    /// </summary>
    public Type? EncryptionProviderType { get; set; }

    /// <summary>
    ///     Gets or sets the memory stream manager for serialization.
    /// </summary>
    public RecyclableMemoryStreamManager RecyclableMemoryStreamManager { get; set; } = new();

    /// <summary>
    ///     Gets or sets the type of stream serializer for serialization.
    /// </summary>
    public Type? StreamSerializerType { get; set; }

    /// <summary>
    ///     Gets or sets the JSON serializer options.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }

    /// <summary>
    ///     Gets or sets the MessagePack serializer options.
    /// </summary>
    public MessagePackSerializerOptions? MessagePackSerializerOptions { get; set; }

    /// <summary>
    ///     Gets or sets the path to the public key file.
    /// </summary>
    public string PublicKeyPath { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the path to the private key file.
    /// </summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>
    ///     Validates the configuration to ensure required properties are set.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if a required property is not set.</exception>
    public void Validate()
    {
        if (SerializerType == null)
        {
            throw new InvalidOperationException("SerializerType must be specified in the configuration.");
        }

        if (StreamSerializerType == null)
        {
            throw new InvalidOperationException("StreamSerializerType must be specified in the configuration.");
        }

        // Add additional validation as needed
    }

    /// <summary>
    ///     Sets the serializer type and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <typeparam name="T">The type of serializer.</typeparam>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithSerializerType<T>()
    {
        SerializerType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Sets the compression provider type and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <typeparam name="T">The type of compression provider.</typeparam>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithCompressionProviderType<T>()
    {
        CompressionProviderType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Sets the encoding provider type and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <typeparam name="T">The type of encoding provider.</typeparam>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithEncodingProviderType<T>()
    {
        EncodingProviderType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Sets the encryption provider type and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <typeparam name="T">The type of encryption provider.</typeparam>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithEncryptionProviderType<T>()
    {
        EncryptionProviderType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Sets the stream serializer type and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <typeparam name="T">The type of stream serializer.</typeparam>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithStreamSerializerType<T>()
    {
        StreamSerializerType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Sets the JSON serializer options and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <param name="options">The JSON serializer options.</param>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithJsonSerializerOptions(JsonSerializerOptions options)
    {
        JsonSerializerOptions = options;
        return this;
    }

    /// <summary>
    ///     Sets the MessagePack serializer options and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <param name="options">The MessagePack serializer options.</param>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithMessagePackSerializerOptions(MessagePackSerializerOptions options)
    {
        MessagePackSerializerOptions = options;
        return this;
    }

    /// <summary>
    ///     Sets the public key path and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <param name="path">The public key file path.</param>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithPublicKeyPath(string path)
    {
        PublicKeyPath = path;
        return this;
    }

    /// <summary>
    ///     Sets the private key path and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <param name="path">The private key file path.</param>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithPrivateKeyPath(string path)
    {
        PrivateKeyPath = path;
        return this;
    }
}
