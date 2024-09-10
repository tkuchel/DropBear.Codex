#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Models;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Builders;

/// <summary>
///     Builder class for creating instances of <see cref="DropBearFile" /> with various properties and content.
/// </summary>
[SupportedOSPlatform("windows")]
public class DropBearFileBuilder
{
    private readonly List<ContentContainer> _contentContainers = new();
    private readonly ILogger _logger;
    private readonly Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private FileVersion? _currentVersion;

    private DropBearFile? _file; // Keeps track of DropBearFile instance
    private string? _fileName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DropBearFileBuilder" /> class.
    /// </summary>
    public DropBearFileBuilder()
    {
        _logger = LoggerFactory.Logger.ForContext<DropBearFileBuilder>();
    }

    /// <summary>
    ///     Sets the file name for the DropBearFile.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>The current <see cref="DropBearFileBuilder" /> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when fileName is null or empty.</exception>
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
    ///     Sets the version for the DropBearFile.
    /// </summary>
    /// <param name="versionLabel">The label of the version.</param>
    /// <param name="versionDate">The date of the version.</param>
    /// <returns>The current <see cref="DropBearFileBuilder" /> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when versionLabel is null or empty.</exception>
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
    ///     Adds metadata to the DropBearFile.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>The current <see cref="DropBearFileBuilder" /> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when key or value is null or empty.</exception>
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
    ///     Adds a ContentContainer to the DropBearFile.
    /// </summary>
    /// <param name="container">The ContentContainer to add.</param>
    /// <returns>The current <see cref="DropBearFileBuilder" /> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when container is null.</exception>
    public DropBearFileBuilder AddContentContainer(ContentContainer container)
    {
        try
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container), "ContentContainer cannot be null.");
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
    ///     Builds and returns the configured DropBearFile instance.
    /// </summary>
    /// <returns>The built <see cref="DropBearFile" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the DropBearFile is in an invalid state.</exception>
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

            _file = new DropBearFile(_metadata, _contentContainers, _fileName, _currentVersion);

            _logger.Information("Built DropBearFile: {FileName}", _file.FileName);
            return _file;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error building DropBearFile");
            throw;
        }
    }
}
