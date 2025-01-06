#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Models;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Builders;

/// <summary>
///     A builder class for creating <see cref="DropBearFile" /> instances,
///     including metadata, version info, and content containers.
/// </summary>
[SupportedOSPlatform("windows")]
public class DropBearFileBuilder
{
    private readonly List<ContentContainer> _contentContainers = new();
    private readonly ILogger _logger;
    private readonly Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private FileVersion? _currentVersion;
    private string? _fileName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DropBearFileBuilder" /> class.
    /// </summary>
    public DropBearFileBuilder()
    {
        _logger = LoggerFactory.Logger.ForContext<DropBearFileBuilder>();
    }

    /// <summary>
    ///     Sets the filename for the resulting <see cref="DropBearFile" />.
    /// </summary>
    /// <param name="fileName">The file name to assign.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="fileName" /> is null or empty.</exception>
    public DropBearFileBuilder WithFileName(string fileName)
    {
        try
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
            }

            _fileName = fileName;
            _logger.Debug("Set file name: {FileName}", fileName);
            return this;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error setting file name");
            throw;
        }
    }

    /// <summary>
    ///     Sets the version information for the resulting <see cref="DropBearFile" />.
    /// </summary>
    /// <param name="versionLabel">The version label (e.g. "1.0.0").</param>
    /// <param name="versionDate">A <see cref="DateTimeOffset" /> indicating the version date.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="versionLabel" /> is null or empty.</exception>
    public DropBearFileBuilder WithVersion(string versionLabel, DateTimeOffset versionDate)
    {
        try
        {
            if (string.IsNullOrEmpty(versionLabel))
            {
                throw new ArgumentException("Version label cannot be null or empty.", nameof(versionLabel));
            }

            _currentVersion = new FileVersion(versionLabel, versionDate);
            _logger.Debug("Set file version: {VersionLabel} ({VersionDate})", versionLabel, versionDate);
            return this;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error setting file version");
            throw;
        }
    }

    /// <summary>
    ///     Adds metadata (key-value pairs) to the resulting <see cref="DropBearFile" />.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key" /> or <paramref name="value" /> is null or empty.</exception>
    public DropBearFileBuilder AddMetadata(string key, string value)
    {
        try
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Metadata key cannot be null or empty.", nameof(key));
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Metadata value cannot be null or empty.", nameof(value));
            }

            if (_metadata.ContainsKey(key))
            {
                _logger.Warning("Overwriting existing metadata for key: {Key}", key);
            }

            _metadata[key] = value;
            _logger.Debug("Added metadata: {Key} = {Value}", key, value);
            return this;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error adding metadata");
            throw;
        }
    }

    /// <summary>
    ///     Adds a pre-built <see cref="ContentContainer" /> to the resulting <see cref="DropBearFile" />.
    /// </summary>
    /// <param name="container">The <see cref="ContentContainer" /> to include.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="container" /> is null.</exception>
    public DropBearFileBuilder AddContentContainer(ContentContainer container)
    {
        try
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container), "ContentContainer cannot be null.");
            }

            // Optionally verify the container is built (e.g., container.Data != null)
            if (container.Data == null)
            {
                _logger.Warning("The ContentContainer has no data set. Ensure it is built properly.");
            }

            _contentContainers.Add(container);
            _logger.Debug("Added ContentContainer");
            return this;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error adding ContentContainer");
            throw;
        }
    }

    /// <summary>
    ///     Builds and returns a <see cref="DropBearFile" /> instance with all configured metadata, version, and content
    ///     containers.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if a file name or version is not set.</exception>
    public DropBearFile Build()
    {
        try
        {
            if (string.IsNullOrEmpty(_fileName))
            {
                throw new InvalidOperationException("FileName must be set before building.");
            }

            if (_currentVersion == null)
            {
                throw new InvalidOperationException("Version must be set before building.");
            }

            var file = new DropBearFile(_metadata, _contentContainers, _fileName, _currentVersion);
            _logger.Information("Built DropBearFile: {FileName}", file.FileName);
            return file;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error building DropBearFile");
            throw;
        }
    }
}
