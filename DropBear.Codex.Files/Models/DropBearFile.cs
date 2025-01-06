#region

using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;

#endregion

namespace DropBear.Codex.Files.Models;

/// <summary>
///     Represents a file containing multiple <see cref="ContentContainer" /> objects and optional metadata.
///     Each <see cref="ContentContainer" /> can hold compressed/encrypted/serialized data.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DropBearFile
{
    private const string DefaultExtension = ".dbf";

    private static readonly IEqualityComparer<KeyValuePair<string, string>> MetadataComparer =
        new MetadataEqualityComparer();

    /// <summary>
    ///     Initializes a new instance of the <see cref="DropBearFile" /> class
    ///     with default (empty) metadata, content containers, and filename.
    /// </summary>
    public DropBearFile()
    {
        ContentContainers = new Collection<ContentContainer>();
        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FileName = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DropBearFile" /> class via JSON constructor.
    /// </summary>
    /// <param name="metadata">Optional key-value metadata for this file.</param>
    /// <param name="contentContainers">Optional collection of <see cref="ContentContainer" /> objects.</param>
    /// <param name="fileName">The filename or identifier.</param>
    /// <param name="currentVersion">Optional versioning info.</param>
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
    ///     Gets the file name (or identifier) for this <see cref="DropBearFile" />.
    /// </summary>
    public string FileName { get; init; }

    /// <summary>
    ///     Gets an editable dictionary of metadata for this file.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IDictionary<string, string> Metadata { get; init; }

    /// <summary>
    ///     Gets a collection of <see cref="ContentContainer" /> objects embedded in this file.
    /// </summary>
    [JsonPropertyName("contentContainers")]
    public ICollection<ContentContainer> ContentContainers { get; init; }

    /// <summary>
    ///     Gets the version information for this file (if any).
    /// </summary>
    [JsonPropertyName("currentVersion")]
    public FileVersion? CurrentVersion { get; init; }

    /// <summary>
    ///     Returns the default extension for <see cref="DropBearFile" /> files (.dbf).
    /// </summary>
    public static string GetDefaultExtension()
    {
        return DefaultExtension;
    }

    /// <summary>
    ///     Adds a new metadata key-value pair.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <exception cref="ArgumentException">Thrown if the key already exists.</exception>
    public void AddMetadata(string key, string value)
    {
        if (!Metadata.TryAdd(key, value))
        {
            throw new ArgumentException($"Duplicate metadata key: {key}.", nameof(key));
        }
    }

    /// <summary>
    ///     Removes a metadata key-value pair.
    /// </summary>
    /// <param name="key">The metadata key to remove.</param>
    /// <exception cref="ArgumentException">Thrown if the key does not exist.</exception>
    public void RemoveMetadata(string key)
    {
        if (!Metadata.Remove(key))
        {
            throw new ArgumentException($"Metadata key not found: {key}.", nameof(key));
        }
    }

    /// <summary>
    ///     Adds a <see cref="ContentContainer" /> to this file.
    /// </summary>
    /// <param name="container">The container to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="container" /> is null.</exception>
    public void AddContentContainer(ContentContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        ContentContainers.Add(container);
    }

    /// <summary>
    ///     Removes a <see cref="ContentContainer" /> from this file.
    /// </summary>
    /// <param name="container">The container to remove.</param>
    /// <exception cref="ArgumentException">Thrown if the container is not found.</exception>
    public void RemoveContentContainer(ContentContainer container)
    {
        if (!ContentContainers.Remove(container))
        {
            throw new ArgumentException("Content container not found.", nameof(container));
        }
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is DropBearFile other)
        {
            return string.Equals(FileName, other.FileName, StringComparison.Ordinal) &&
                   CurrentVersion?.Equals(other.CurrentVersion) == true &&
                   Metadata.SequenceEqual(other.Metadata, MetadataComparer) &&
                   ContentContainers.SequenceEqual(other.ContentContainers);
        }

        return false;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FileName);
        hash.Add(CurrentVersion);

        // Incorporate metadata
        foreach (var (key, value) in Metadata)
        {
            hash.Add(key, StringComparer.OrdinalIgnoreCase);
            hash.Add(value);
        }

        // Incorporate content containers
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
                StringComparer.Ordinal.GetHashCode(obj.Value));
        }
    }
}
