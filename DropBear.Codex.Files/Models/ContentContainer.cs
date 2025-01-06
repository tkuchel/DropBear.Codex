#region

using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Files.Enums;
using DropBear.Codex.Hashing;
using DropBear.Codex.Hashing.Interfaces;
using DropBear.Codex.Serialization.Factories;
using DropBear.Codex.Utilities.Extensions;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Models;

/// <summary>
///     Represents a container holding data (in raw bytes or serialized form),
///     along with information about how it should be processed (compression, encryption, etc.).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ContentContainer
{
    private static readonly ILogger Logger =
        LoggerFactory.Logger.ForContext<SerializationBuilder>();

    private readonly IHasher _hasher = new HashBuilder().GetHasher("XxHash");
    private readonly Dictionary<string, Type> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentContainer" /> class.
    ///     Defaults to <see cref="Flags" /> = <see cref="ContentContainerFlags.NoOperation" />.
    /// </summary>
    public ContentContainer()
    {
        Flags = ContentContainerFlags.NoOperation;
        ContentType = "Unsupported/Unknown DataType";
    }

    /// <summary>
    ///     Gets or sets the container flags indicating which operations
    ///     (compression, encryption, serialization, etc.) apply to this container.
    /// </summary>
    [JsonPropertyName("flags")]
    public ContentContainerFlags Flags { get; private set; }

    /// <summary>
    ///     Gets or sets the content type (e.g., "application/json") or the type name of the data.
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; private set; }

    /// <summary>
    ///     Gets or sets the raw data (byte array) for this container.
    ///     If <see cref="TemporaryData" /> is set, this may be created after serialization.
    /// </summary>
    [JsonPropertyName("data")]
    public byte[]? Data { get; internal set; }

    /// <summary>
    ///     Gets or sets the base64-encoded hash for <see cref="Data" />, used for integrity checks.
    /// </summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; private set; }

    /// <summary>
    ///     Gets the optional in-memory object to be serialized.
    ///     If set, <see cref="Flags" /> should contain <see cref="ContentContainerFlags.ShouldSerialize" />.
    /// </summary>
    public object? TemporaryData { get; private set; }

    /// <summary>
    ///     Determines if this container requires serialization (based on <see cref="Flags" />).
    /// </summary>
    public bool RequiresSerialization()
    {
        return Flags.HasFlag(ContentContainerFlags.ShouldSerialize);
    }

    /// <summary>
    ///     Determines if this container requires compression (based on <see cref="Flags" />).
    /// </summary>
    public bool RequiresCompression()
    {
        return Flags.HasFlag(ContentContainerFlags.ShouldCompress);
    }

    /// <summary>
    ///     Determines if this container requires encryption (based on <see cref="Flags" />).
    /// </summary>
    public bool RequiresEncryption()
    {
        return Flags.HasFlag(ContentContainerFlags.ShouldEncrypt);
    }

    /// <summary>
    ///     Sets the data for this container. If <typeparamref name="T" /> is a byte array or string,
    ///     the data is stored directly. Otherwise, it is placed in <see cref="TemporaryData" /> and
    ///     flagged for serialization.
    /// </summary>
    /// <typeparam name="T">The data type being stored.</typeparam>
    /// <param name="data">The data to store.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public Result SetData<T>(T? data)
    {
        if (data is null)
        {
            return Result.Failure("Data is null.");
        }

        switch (data)
        {
            case byte[] byteArray:
                Data = byteArray;
                Flags |= ContentContainerFlags.DataIsSet;
                break;

            case string str:
                Data = Encoding.UTF8.GetBytes(str);
                Flags |= ContentContainerFlags.DataIsSet;
                break;

            default:
                TemporaryData = data;
                Flags |= ContentContainerFlags.TemporaryDataIsSet | ContentContainerFlags.ShouldSerialize;
                break;
        }

        ContentType = typeof(T).FullName ?? "Unsupported/Unknown DataType";

        // If data was set directly, compute a hash
        if (Data is not null)
        {
            ComputeAndSetHash();
        }

        return Result.Success();
    }

    /// <summary>
    ///     Adds a provider (serializer, compressor, or encryptor) by name and <see cref="Type" />.
    /// </summary>
    /// <param name="key">E.g., "Serializer", "CompressionProvider", "EncryptionProvider".</param>
    /// <param name="type">The concrete <see cref="Type" /> implementing the provider.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key" /> or <paramref name="type" /> is invalid.</exception>
    public void AddProvider(string key, Type type)
    {
        if (string.IsNullOrEmpty(key) || type is null)
        {
            throw new ArgumentException("Key and Type must not be null or empty.", nameof(key));
        }

        if (!_providers.TryAdd(key, type))
        {
            throw new ArgumentException(
                $"An item with the key '{key}' has already been added.",
                nameof(key));
        }
    }

    /// <summary>
    ///     Replaces the current providers dictionary with the given dictionary.
    /// </summary>
    /// <param name="providers">A dictionary of provider name to <see cref="Type" />.</param>
    internal void SetProviders(Dictionary<string, Type> providers)
    {
        foreach (var provider in providers)
        {
            _providers[provider.Key] = provider.Value;
        }
    }

    /// <summary>
    ///     Gets a copy of the providers dictionary, mapping e.g. "Serializer" to a <see cref="Type" />.
    /// </summary>
    /// <returns>A new dictionary of providers.</returns>
    public IDictionary<string, Type> GetProvidersDictionary()
    {
        return new Dictionary<string, Type>(_providers, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Computes and sets the <see cref="Hash" /> property for <see cref="Data" /> using the configured
    ///     <see cref="IHasher" />.
    /// </summary>
    internal void ComputeAndSetHash()
    {
        if (Data is null)
        {
            return;
        }

        var hashResult = _hasher.EncodeToBase64Hash(Data);
        Hash = hashResult.IsSuccess ? hashResult.Value : null;
    }

    /// <summary>
    ///     Asynchronously gets the data as type <typeparamref name="T" />, verifying integrity with <see cref="Hash" />.
    ///     If needed, a configured serializer is used to deserialize <see cref="Data" /> into <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The desired output type (e.g., <c>byte[]</c>, <c>string</c>, or a model class).</typeparam>
    /// <returns>A <see cref="Result{T}" /> with the requested data or an error.</returns>
    public async Task<Result<T>> GetDataAsync<T>()
    {
        if (Data is null || Data.Length == 0)
        {
            return Result<T>.Failure("No data available.");
        }

        if (Hash is null)
        {
            return Result<T>.Failure("No hash available.");
        }

        // Check data integrity
        var hashResult = _hasher.EncodeToBase64Hash(Data);
        if (!hashResult.IsSuccess || !string.Equals(hashResult.Value, Hash, StringComparison.Ordinal))
        {
            return Result<T>.Failure("Data integrity check failed.");
        }

        // If type is a byte array and no serialization is needed
        if (typeof(T) == typeof(byte[]))
        {
            return IsFlagEnabled(ContentContainerFlags.NoSerialization)
                ? Result<T>.Success((T)(object)Data)
                : Result<T>.Failure("No serialization required, but type is byte[].");
        }

        // Build serializer if needed
        var serializerBuilder = new SerializationBuilder();
        ConfigureContainerSerializer(serializerBuilder);
        var serializer = RequiresSerialization() ? serializerBuilder.Build() : null;

        try
        {
            if (serializer is null)
            {
                return Result<T>.Failure("No serializer configured.");
            }

            var result = await serializer.DeserializeAsync<T>(Data).ConfigureAwait(false);
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure($"Deserialization failed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Configures a <see cref="SerializationBuilder" /> with providers (serializer, compressor, encryptor)
    ///     indicated by <see cref="Flags" />.
    /// </summary>
    /// <param name="serializerBuilder">The builder to configure.</param>
    internal void ConfigureContainerSerializer(SerializationBuilder serializerBuilder)
    {
        try
        {
            if (RequiresSerialization())
            {
                var serializerType = GetProviderType("Serializer");
                serializerBuilder.WithSerializer(serializerType);
            }

            if (RequiresCompression())
            {
                var compressionType = GetProviderType("CompressionProvider");
                serializerBuilder.WithCompression(compressionType);
            }

            if (RequiresEncryption())
            {
                var encryptionType = GetProviderType("EncryptionProvider");
                serializerBuilder.WithEncryption(encryptionType);
            }
        }
        catch (KeyNotFoundException ex)
        {
            Console.WriteLine(ex.Message);
            Logger.Error(ex, "Error while configuring ContentContainer serializer: {Message}", ex.Message);
            throw; // rethrow to preserve original exception details
        }
    }

    /// <summary>
    ///     Gets the provider <see cref="Type" /> from the dictionary by key name (e.g., "Serializer").
    /// </summary>
    /// <param name="keyName">The provider key name.</param>
    /// <exception cref="KeyNotFoundException">Thrown if no provider is found for the given key.</exception>
    private Type GetProviderType(string keyName)
    {
        if (_providers.TryGetValue(keyName, out var type))
        {
            Console.WriteLine($"Provider for {keyName} found: {type.Name}");
            Logger.Debug("Provider for {ProviderKey} found: {ProviderType}", keyName, type.Name);
            return type;
        }

        var errorMessage = $"Provider for {keyName} not found.";
        Logger.Error(errorMessage);
        Console.WriteLine(errorMessage);
        throw new KeyNotFoundException(errorMessage);
    }

    /// <summary>
    ///     Enables a specific <see cref="ContentContainerFlags" /> on this container.
    /// </summary>
    /// <param name="flag">A <see cref="ContentContainerFlags" /> enum value to enable.</param>
    public void EnableFlag(ContentContainerFlags flag)
    {
        Flags |= flag;
    }

    /// <summary>
    ///     Disables a specific <see cref="ContentContainerFlags" /> on this container.
    /// </summary>
    /// <param name="flag">A <see cref="ContentContainerFlags" /> enum value to disable.</param>
    public void DisableFlag(ContentContainerFlags flag)
    {
        Flags &= ~flag;
    }

    /// <summary>
    ///     Checks if a particular <see cref="ContentContainerFlags" /> is enabled.
    /// </summary>
    private bool IsFlagEnabled(ContentContainerFlags flag)
    {
        return (Flags & flag) == flag;
    }

    /// <summary>
    ///     Logs the currently enabled <see cref="ContentContainerFlags" /> for debugging purposes.
    /// </summary>
    public void PrintFlags()
    {
        Logger.Information("Current Features Enabled:");
        foreach (ContentContainerFlags flag in Enum.GetValues(typeof(ContentContainerFlags)))
        {
            if (IsFlagEnabled(flag))
            {
                Console.WriteLine($"- {flag}");
            }
        }
    }

    /// <summary>
    ///     Sets the <see cref="ContentType" /> for this container.
    /// </summary>
    /// <param name="type">A string describing the content type.</param>
    internal void SetContentType(string type)
    {
        ContentType = type;
    }

    /// <summary>
    ///     Sets the <see cref="Hash" /> of the container data externally.
    /// </summary>
    /// <param name="hash">A base64-encoded hash string.</param>
    internal void SetHash(string? hash)
    {
        Hash = hash;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is ContentContainer other)
        {
            return string.Equals(Hash, other.Hash, StringComparison.Ordinal) &&
                   Equals(Data, other.Data);
        }

        return false;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        // Using extension methods to ensure read-only usage
        return HashCode.Combine(Hash.GetReadOnlyVersion(), Data.GetReadOnlyVersion());
    }
}
