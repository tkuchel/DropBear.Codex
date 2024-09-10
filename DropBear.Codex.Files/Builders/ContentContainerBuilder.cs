#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Enums;
using DropBear.Codex.Files.Models;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Builders;

/// <summary>
///     Builder class for creating instances of <see cref="ContentContainer" /> with various properties and content.
/// </summary>
[SupportedOSPlatform("windows")]
public class ContentContainerBuilder
{
    private readonly ContentContainer _container = new();
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentContainerBuilder" /> class.
    /// </summary>
    public ContentContainerBuilder()
    {
        _logger = LoggerFactory.Logger.ForContext<ContentContainerBuilder>();
    }

    /// <summary>
    ///     Sets the data for the ContentContainer.
    /// </summary>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <param name="data">The data to set.</param>
    /// <returns>The current <see cref="ContentContainerBuilder" /> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    public ContentContainerBuilder WithData<T>(T data)
    {
        try
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "Data cannot be null.");
            }

            var result = _container.SetData(data);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to set data: {result.ErrorMessage}");
            }

            _logger.Debug("Set data of type {DataType}", typeof(T).Name);
            return this;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error setting data");
            throw;
        }
    }

    /// <summary>
    ///     Enables a flag on the ContentContainer.
    /// </summary>
    /// <param name="flag">The flag to enable.</param>
    /// <returns>The current <see cref="ContentContainerBuilder" /> instance.</returns>
    public ContentContainerBuilder WithFlag(ContentContainerFlags flag)
    {
        try
        {
            _container.EnableFlag(flag);
            _logger.Debug("Enabled flag: {Flag}", flag);
            return this;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error enabling flag");
            throw;
        }
    }

    /// <summary>
    ///     Sets the content type for the ContentContainer.
    /// </summary>
    /// <param name="contentType">The content type to set.</param>
    /// <returns>The current <see cref="ContentContainerBuilder" /> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when contentType is null or empty.</exception>
    public ContentContainerBuilder WithContentType(string contentType)
    {
        try
        {
            if (string.IsNullOrEmpty(contentType))
            {
                throw new ArgumentException("Content type cannot be null or empty.", nameof(contentType));
            }

            _container.SetContentType(contentType);
            _logger.Debug("Set content type: {ContentType}", contentType);
            return this;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error setting content type");
            throw;
        }
    }

    /// <summary>
    ///     Adds a provider to the ContentContainer.
    /// </summary>
    /// <param name="key">The key for the provider.</param>
    /// <param name="providerType">The type of the provider.</param>
    /// <returns>The current <see cref="ContentContainerBuilder" /> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when key is null or empty, or when providerType is null.</exception>
    public ContentContainerBuilder AddProvider(string key, Type providerType)
    {
        try
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Provider key cannot be null or empty.", nameof(key));
            }

            if (providerType == null)
            {
                throw new ArgumentNullException(nameof(providerType), "Provider type cannot be null.");
            }

            _container.AddProvider(key, providerType);
            _logger.Debug("Added provider: {ProviderKey} of type {ProviderType}", key, providerType.Name);
            return this;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error adding provider");
            throw;
        }
    }

    /// <summary>
    ///     Builds and returns the configured ContentContainer instance.
    /// </summary>
    /// <returns>The built <see cref="ContentContainer" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the ContentContainer is in an invalid state.</exception>
    public ContentContainer Build()
    {
        try
        {
            if (_container.Data == null && _container.TemporaryData == null)
            {
                throw new InvalidOperationException("Data must be set before building.");
            }

            _logger.Information("Built ContentContainer");
            return _container;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error building ContentContainer");
            throw;
        }
    }
}
