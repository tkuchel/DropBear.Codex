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

    public DropBearFile()
    {
        ContentContainers = new Collection<ContentContainer>();
        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    ///     Gets or sets the file name, which includes the extension.
    /// </summary>
    public string FileName { get; } = string.Empty;

    /// <summary>
    ///     Gets the metadata associated with the file.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IDictionary<string, string> Metadata { get; init; }

    /// <summary>
    ///     Gets or sets the content containers within the file.
    /// </summary>
    [JsonPropertyName("contentContainers")]
    public ICollection<ContentContainer> ContentContainers { get; set; }

    /// <summary>
    ///     Gets or sets the current version of the file.
    /// </summary>
    [JsonPropertyName("currentVersion")]
    public FileVersion? CurrentVersion { get; }

    /// <summary>
    ///     Gets the default extension used for DropBear files.
    /// </summary>
    public static string GetDefaultExtension()
    {
        return DefaultExtension;
    }

    /// <summary>
    ///     Adds metadata to the file.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <exception cref="ArgumentException">Thrown when the key already exists.</exception>
    public void AddMetadata(string key, string value)
    {
        if (!Metadata.TryAdd(key, value))
        {
            throw new ArgumentException($"Duplicate metadata key: {key}.", nameof(key));
        }
    }

    /// <summary>
    ///     Removes metadata from the file.
    /// </summary>
    /// <param name="key">The metadata key to remove.</param>
    /// <exception cref="ArgumentException">Thrown when the key does not exist.</exception>
    public void RemoveMetadata(string key)
    {
        if (!Metadata.Remove(key))
        {
            throw new ArgumentException($"Metadata key not found: {key}.", nameof(key));
        }
    }

    /// <summary>
    ///     Adds a content container to the file.
    /// </summary>
    /// <param name="container">The content container to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when the container is null.</exception>
    public void AddContentContainer(ContentContainer container)
    {
        ArgumentNullException.ThrowIfNull(container, nameof(container));
        ContentContainers.Add(container);
    }

    /// <summary>
    ///     Removes a content container from the file.
    /// </summary>
    /// <param name="container">The content container to remove.</param>
    /// <exception cref="ArgumentException">Thrown when the container does not exist in the collection.</exception>
    public void RemoveContentContainer(ContentContainer container)
    {
        if (!ContentContainers.Remove(container))
        {
            throw new ArgumentException("Content container not found.", nameof(container));
        }
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is DropBearFile other &&
               string.Equals(FileName, other.FileName, StringComparison.Ordinal) &&
               CurrentVersion?.Equals(other.CurrentVersion) == true &&
               Metadata.SequenceEqual(other.Metadata) &&
               ContentContainers.SequenceEqual(other.ContentContainers);
    }

    /// <summary>
    ///     Serves as the default hash function.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FileName);
        hash.Add(CurrentVersion);

        foreach (var item in Metadata)
        {
            hash.Add(item.Key);
            hash.Add(item.Value);
        }

        foreach (var container in ContentContainers)
        {
            hash.Add(container);
        }

        return hash.ToHashCode();
    }
}
