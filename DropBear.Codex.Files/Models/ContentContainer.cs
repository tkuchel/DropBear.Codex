#region

using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Files.Enums;
using DropBear.Codex.Files.Errors;
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

    private readonly IHasher _hasher;
    private readonly Dictionary<string, Type> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentContainer" /> class.
    ///     Defaults to <see cref="Flags" /> = <see cref="ContentContainerFlags.NoOperation" />.
    /// </summary>
    public ContentContainer()
        : this(new HashBuilder().GetHasher("XxHash"))
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentContainer" /> class with a custom hasher.
    /// </summary>
    /// <param name="hasher">The hasher to use for hash computations.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="hasher" /> is null.</exception>
    public ContentContainer(IHasher hasher)
    {
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
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
    /// <returns><c>true</c> if serialization is required; otherwise, <c>false</c>.</returns>
    public bool RequiresSerialization()
    {
        return Flags.HasFlag(ContentContainerFlags.ShouldSerialize);
    }

    /// <summary>
    ///     Determines if this container requires compression (based on <see cref="Flags" />).
    /// </summary>
    /// <returns><c>true</c> if compression is required; otherwise, <c>false</c>.</returns>
    public bool RequiresCompression()
    {
        return Flags.HasFlag(ContentContainerFlags.ShouldCompress);
    }

    /// <summary>
    ///     Determines if this container requires encryption (based on <see cref="Flags" />).
    /// </summary>
    /// <returns><c>true</c> if encryption is required; otherwise, <c>false</c>.</returns>
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
    /// <returns>A <see cref="Result{Unit, ContentContainerError}" /> indicating success or failure.</returns>
    public Result<Unit, ContentContainerError> SetData<T>(T? data)
    {
        if (data is null)
        {
            return Result<Unit, ContentContainerError>.Failure(
                ContentContainerError.InvalidData);
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

        return Result<Unit, ContentContainerError>.Success(Unit.Value);
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
        if (hashResult.IsSuccess)
        {
            Hash = hashResult.Value;
        }
        else
        {
            Logger.Warning("Failed to compute hash for data: {ErrorMessage}",
                hashResult.Error?.Message ?? "Unknown error");
            Hash = null;
        }
    }

    /// <summary>
    ///     Asynchronously gets the data as type <typeparamref name="T" />, verifying integrity with <see cref="Hash" />.
    ///     If needed, a configured serializer is used to deserialize <see cref="Data" /> into <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The desired output type (e.g., <c>byte[]</c>, <c>string</c>, or a model class).</typeparam>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{T, ContentContainerError}" /> with the requested data or an error.</returns>
    public async ValueTask<Result<T, ContentContainerError>> GetDataAsync<T>(
        CancellationToken cancellationToken = default)
    {
        if (Data is null || Data.Length == 0)
        {
            return Result<T, ContentContainerError>.Failure(ContentContainerError.InvalidData);
        }

        if (Hash is null)
        {
            return Result<T, ContentContainerError>.Failure(
                new ContentContainerError("No hash available for data integrity verification."));
        }

        // Check data integrity
        var hashResult = _hasher.EncodeToBase64Hash(Data);
        if (!hashResult.IsSuccess || !string.Equals(hashResult.Value, Hash, StringComparison.Ordinal))
        {
            return Result<T, ContentContainerError>.Failure(ContentContainerError.HashVerificationFailed);
        }

        // If type is a byte array and no serialization is needed
        if (typeof(T) == typeof(byte[]))
        {
            return IsFlagEnabled(ContentContainerFlags.NoSerialization)
                ? Result<T, ContentContainerError>.Success((T)(object)Data)
                : Result<T, ContentContainerError>.Failure(
                    new ContentContainerError("No serialization required, but type is byte[]."));
        }

        // Build serializer if needed
        var serializerBuilder = new SerializationBuilder();
        ConfigureContainerSerializer(serializerBuilder);

        try
        {
            var serializer = RequiresSerialization() ? serializerBuilder.Build() : null;

            if (serializer is null)
            {
                return Result<T, ContentContainerError>.Failure(
                    new ContentContainerError("No serializer configured."));
            }

            // Use cancellation token for deserialization
            cancellationToken.ThrowIfCancellationRequested();

            // Check we got a serializer and not an error
            if (!serializer.IsSuccess)
            {
                return Result<T, ContentContainerError>.Failure(
                    new ContentContainerError("Failed to build serializer."));
            }

            var result = await serializer.Value!.DeserializeAsync<T>(Data, cancellationToken).ConfigureAwait(false);

            return !result.IsSuccess
                ? Result<T, ContentContainerError>.Failure(new ContentContainerError(result.Exception?.Message))
                : Result<T, ContentContainerError>.Success(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw; // Let cancellation propagate
        }
        catch (Exception ex)
        {
            return Result<T, ContentContainerError>.Failure(
                new ContentContainerError($"Deserialization failed: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Configures a <see cref="SerializationBuilder" /> with providers (serializer, compressor, encryptor)
    ///     indicated by <see cref="Flags" />.
    /// </summary>
    /// <param name="serializerBuilder">The builder to configure.</param>
    /// <exception cref="KeyNotFoundException">Thrown if a required provider is not found.</exception>
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
            Logger.Error(ex, "Error while configuring ContentContainer serializer: {Message}", ex.Message);
            throw; // rethrow to preserve original exception details
        }
    }

    /// <summary>
    ///     Gets the provider <see cref="Type" /> from the dictionary by key name (e.g., "Serializer").
    /// </summary>
    /// <param name="keyName">The provider key name.</param>
    /// <returns>The provider type if found.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no provider is found for the given key.</exception>
    private Type GetProviderType(string keyName)
    {
        if (_providers.TryGetValue(keyName, out var type))
        {
            Logger.Debug("Provider for {ProviderKey} found: {ProviderType}", keyName, type.Name);
            return type;
        }

        var errorMessage = $"Provider for {keyName} not found.";
        Logger.Error(errorMessage);
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
    /// <param name="flag">The flag to check.</param>
    /// <returns><c>true</c> if the flag is enabled; otherwise, <c>false</c>.</returns>
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
