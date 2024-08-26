#region

using System.Text.Json;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Writers;

/// <summary>
///     Implementation of <see cref="ISerializerWriter" /> for JSON serialization.
/// </summary>
public class JsonSerializerWriter : ISerializerWriter
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<JsonSerializerWriter>();
    private readonly JsonSerializerOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonSerializerWriter" /> class with the specified options.
    /// </summary>
    /// <param name="options">The JSON serializer options.</param>
    public JsonSerializerWriter(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
    {
        _logger.Information("Starting JSON serialization for type {Type}.", typeof(T));

        try
        {
            await JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken).ConfigureAwait(false);
            _logger.Information("JSON serialization completed successfully for type {Type}.", typeof(T));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during JSON serialization for type {Type}.", typeof(T));
            throw;
        }
    }
}
