#region

using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Serialization;
using DropBear.Codex.Core;
using DropBear.Codex.Files.Enums;
using DropBear.Codex.Hashing;
using DropBear.Codex.Hashing.Interfaces;
using DropBear.Codex.Serialization.Factories;
using DropBear.Codex.Utilities.Extensions;

#endregion

namespace DropBear.Codex.Files.Models;

[SupportedOSPlatform("windows")]
public sealed class ContentContainer
{
    private readonly IHasher _hasher = new HashBuilder().GetHasher("XxHash");
    private readonly Dictionary<string, Type> _providers = new(StringComparer.OrdinalIgnoreCase);

    public ContentContainer()
    {
        Flags = ContentContainerFlags.NoOperation;
        ContentType = "Unsupported/Unknown DataType";
    }

    [JsonPropertyName("flags")] public ContentContainerFlags Flags { get; private set; }

    [JsonPropertyName("contentType")] public string ContentType { get; private set; }

    [JsonPropertyName("data")] public byte[]? Data { get; internal set; }

    [JsonPropertyName("hash")] public string? Hash { get; private set; }

    public object? TemporaryData { get; private set; }

    /// <summary>
    ///     Determines if serialization is required based on the flags.
    /// </summary>
    /// <returns>True if serialization is required; otherwise, false.</returns>
    public bool RequiresSerialization()
    {
        return Flags.HasFlag(ContentContainerFlags.ShouldSerialize);
    }

    /// <summary>
    ///     Determines if compression is required based on the flags.
    /// </summary>
    /// <returns>True if compression is required; otherwise, false.</returns>
    public bool RequiresCompression()
    {
        return Flags.HasFlag(ContentContainerFlags.ShouldCompress);
    }

    /// <summary>
    ///     Determines if encryption is required based on the flags.
    /// </summary>
    /// <returns>True if encryption is required; otherwise, false.</returns>
    public bool RequiresEncryption()
    {
        return Flags.HasFlag(ContentContainerFlags.ShouldEncrypt);
    }

    /// <summary>
    ///     Sets the data for the content container.
    /// </summary>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <param name="data">The data to set.</param>
    /// <returns>A <see cref="Result" /> indicating the outcome of the operation.</returns>
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

        if (Data is not null)
        {
            ComputeAndSetHash();
        }

        return Result.Success();
    }

    /// <summary>
    ///     Adds a provider to the content container.
    /// </summary>
    /// <param name="key">The key for the provider.</param>
    /// <param name="type">The type of the provider.</param>
    public void AddProvider(string key, Type type)
    {
        if (key is null || type is null)
        {
            throw new ArgumentException("Key and Type must not be null.", nameof(key));
        }

        if (!_providers.TryAdd(key, type))
        {
            throw new ArgumentException($"An item with the key '{key}' has already been added.", nameof(key));
        }
    }

    /// <summary>
    ///     Sets the providers dictionary. This is useful during deserialization.
    /// </summary>
    /// <param name="providers">The dictionary of providers to set.</param>
    internal void SetProviders(Dictionary<string, Type> providers)
    {
        foreach (var provider in providers)
        {
            _providers[provider.Key] = provider.Value;
        }
    }

    /// <summary>
    ///     Gets the providers dictionary.
    /// </summary>
    /// <returns>A dictionary of providers.</returns>
    public IDictionary<string, Type> GetProvidersDictionary()
    {
        return new Dictionary<string, Type>(_providers, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Computes and sets the hash of the data.
    /// </summary>
    internal void ComputeAndSetHash()
    {
        ComputeHash();
    }

    private void ComputeHash()
    {
        if (Data is null)
        {
            return;
        }

        var hashResult = _hasher.EncodeToBase64Hash(Data.ToArray());
        Hash = hashResult.IsSuccess ? hashResult.Value : null;
    }

    /// <summary>
    ///     Gets the data asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <returns>A <see cref="Result{T}" /> containing the data or an error message.</returns>
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

        var hashResult = _hasher.EncodeToBase64Hash(Data.ToArray());
        if (!hashResult.IsSuccess || !string.Equals(hashResult.Value, Hash, StringComparison.Ordinal))
        {
            return Result<T>.Failure("Data integrity check failed.");
        }

        if (typeof(T) == typeof(byte[]))
        {
            return IsFlagEnabled(ContentContainerFlags.NoSerialization)
                ? Result<T>.Success((T)(object)Data)
                : Result<T>.Failure("No serialization required, but type is byte[].");
        }

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

    internal void ConfigureContainerSerializer(SerializationBuilder serializerBuilder)
    {
        if (RequiresSerialization())
        {
            var serializerProviderType = GetProviderType("Serializer");
            serializerBuilder.WithSerializer(serializerProviderType);
        }

        if (RequiresCompression())
        {
            var compressionProviderType = GetProviderType("CompressionProvider");
            serializerBuilder.WithCompression(compressionProviderType);
        }

        if (RequiresEncryption())
        {
            var encryptionProviderType = GetProviderType("EncryptionProvider");
            serializerBuilder.WithEncryption(encryptionProviderType);
        }
    }

    private Type GetProviderType(string keyName)
    {
        if (_providers.TryGetValue(keyName, out var type))
        {
            return type;
        }

        throw new KeyNotFoundException($"Provider for {keyName} not found.");
    }

    /// <summary>
    ///     Enables a specific flag.
    /// </summary>
    /// <param name="flag">The flag to enable.</param>
    public void EnableFlag(ContentContainerFlags flag)
    {
        Flags |= flag;
    }

    /// <summary>
    ///     Disables a specific flag.
    /// </summary>
    /// <param name="flag">The flag to disable.</param>
    public void DisableFlag(ContentContainerFlags flag)
    {
        Flags &= ~flag;
    }

    private bool IsFlagEnabled(ContentContainerFlags flag)
    {
        return (Flags & flag) == flag;
    }

    /// <summary>
    ///     Prints the currently enabled flags to the console.
    /// </summary>
    public void PrintFlags()
    {
        Console.WriteLine("Current Features Enabled:");
        foreach (ContentContainerFlags flag in Enum.GetValues(typeof(ContentContainerFlags)))
        {
            if (IsFlagEnabled(flag))
            {
                Console.WriteLine("- " + flag);
            }
        }
    }

    /// <summary>
    ///     Sets the content type.
    /// </summary>
    /// <param name="type">The content type.</param>
    internal void SetContentType(string type)
    {
        ContentType = type;
    }

    /// <summary>
    ///     Sets the hash value.
    /// </summary>
    /// <param name="hash">The hash value to set.</param>
    internal void SetHash(string? hash)
    {
        Hash = hash;
    }

    public override bool Equals(object? obj)
    {
        if (obj is ContentContainer other)
        {
            return string.Equals(Hash, other.Hash, StringComparison.Ordinal) && Equals(Data, other.Data);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Hash.GetReadOnlyVersion(), Data.GetReadOnlyVersion());
    }
}
