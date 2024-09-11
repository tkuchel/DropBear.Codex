#region

using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Serialization;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Enums;
using DropBear.Codex.Hashing;
using DropBear.Codex.Hashing.Interfaces;
using DropBear.Codex.Serialization.Factories;
using DropBear.Codex.Utilities.Extensions;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Models;

[SupportedOSPlatform("windows")]
public sealed class ContentContainer
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<SerializationBuilder>();
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

    public bool RequiresSerialization()
    {
        return Flags.HasFlag(ContentContainerFlags.ShouldSerialize);
    }

    public bool RequiresCompression()
    {
        return Flags.HasFlag(ContentContainerFlags.ShouldCompress);
    }

    public bool RequiresEncryption()
    {
        return Flags.HasFlag(ContentContainerFlags.ShouldEncrypt);
    }

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

    public void AddProvider(string key, Type type)
    {
        if (string.IsNullOrEmpty(key) || type is null)
        {
            throw new ArgumentException("Key and Type must not be null or empty.", nameof(key));
        }

        if (!_providers.TryAdd(key, type))
        {
            throw new ArgumentException($"An item with the key '{key}' has already been added.", nameof(key));
        }
    }

    internal void SetProviders(Dictionary<string, Type> providers)
    {
        foreach (var provider in providers)
        {
            _providers[provider.Key] = provider.Value;
        }
    }

    public IDictionary<string, Type> GetProvidersDictionary()
    {
        return new Dictionary<string, Type>(_providers, StringComparer.OrdinalIgnoreCase);
    }

    internal void ComputeAndSetHash()
    {
        if (Data is null)
        {
            return;
        }

        var hashResult = _hasher.EncodeToBase64Hash(Data);
        Hash = hashResult.IsSuccess ? hashResult.Value : null;
    }

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

        var hashResult = _hasher.EncodeToBase64Hash(Data);
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
            throw; // rethrow to preserve the original exception
        }
    }


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


    public void EnableFlag(ContentContainerFlags flag)
    {
        Flags |= flag;
    }

    public void DisableFlag(ContentContainerFlags flag)
    {
        Flags &= ~flag;
    }

    private bool IsFlagEnabled(ContentContainerFlags flag)
    {
        return (Flags & flag) == flag;
    }

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

    internal void SetContentType(string type)
    {
        ContentType = type;
    }

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
