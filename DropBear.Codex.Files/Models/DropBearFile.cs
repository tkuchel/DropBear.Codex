#region

using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;

#endregion

namespace DropBear.Codex.Files.Models;

[SupportedOSPlatform("windows")]
public sealed class DropBearFile
{
    private const string DefaultExtension = ".dbf";

    private static readonly IEqualityComparer<KeyValuePair<string, string>> MetadataComparer =
        new MetadataEqualityComparer();

    public DropBearFile()
    {
        ContentContainers = new Collection<ContentContainer>();
        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FileName = string.Empty;
    }

    [JsonConstructor]
    public DropBearFile(
        IDictionary<string, string>? metadata,
        ICollection<ContentContainer>? contentContainers,
        string fileName,
        FileVersion? currentVersion)
    {
        Metadata = metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ContentContainers = contentContainers ?? new Collection<ContentContainer>();
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        CurrentVersion = currentVersion;
    }

    public string FileName { get; init; }

    [JsonPropertyName("metadata")] public IDictionary<string, string> Metadata { get; init; }

    [JsonPropertyName("contentContainers")]
    public ICollection<ContentContainer> ContentContainers { get; init; }

    [JsonPropertyName("currentVersion")] public FileVersion? CurrentVersion { get; init; }

    public static string GetDefaultExtension()
    {
        return DefaultExtension;
    }

    public void AddMetadata(string key, string value)
    {
        if (!Metadata.TryAdd(key, value))
        {
            throw new ArgumentException($"Duplicate metadata key: {key}.", nameof(key));
        }
    }

    public void RemoveMetadata(string key)
    {
        if (!Metadata.Remove(key))
        {
            throw new ArgumentException($"Metadata key not found: {key}.", nameof(key));
        }
    }

    public void AddContentContainer(ContentContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        ContentContainers.Add(container);
    }

    public void RemoveContentContainer(ContentContainer container)
    {
        if (!ContentContainers.Remove(container))
        {
            throw new ArgumentException("Content container not found.", nameof(container));
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is DropBearFile other &&
               string.Equals(FileName, other.FileName, StringComparison.Ordinal) &&
               CurrentVersion?.Equals(other.CurrentVersion) == true &&
               Metadata.SequenceEqual(other.Metadata, MetadataComparer) &&
               ContentContainers.SequenceEqual(other.ContentContainers);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FileName);
        hash.Add(CurrentVersion);

        foreach (var (key, value) in Metadata)
        {
            hash.Add(key, StringComparer.OrdinalIgnoreCase);
            hash.Add(value);
        }

        foreach (var container in ContentContainers)
        {
            hash.Add(container);
        }

        return hash.ToHashCode();
    }

    private sealed class MetadataEqualityComparer : IEqualityComparer<KeyValuePair<string, string>>
    {
        public bool Equals(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
        {
            return string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Value, y.Value, StringComparison.Ordinal);
        }

        public int GetHashCode(KeyValuePair<string, string> obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key),
                StringComparer.Ordinal.GetHashCode(obj.Value)
            );
        }
    }
}
