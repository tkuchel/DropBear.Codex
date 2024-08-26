#region

using System.Text.Json;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Writers;

/// <summary>
///     Implementation of <see cref="ISerializerReader" /> for JSON serialization.
/// </summary>
public class JsonSerializerReader : ISerializerReader
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<JsonSerializerReader>();
    private readonly JsonSerializerOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonSerializerReader" /> class with the specified options.
    /// </summary>
    /// <param name="options">The JSON serializer options.</param>
    public JsonSerializerReader(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        _logger.Information("Starting JSON deserialization for type {Type}.", typeof(T));

        try
        {
            var result = await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken)
                .ConfigureAwait(false);

            if (result is null)
            {
                _logger.Warning("JSON deserialization resulted in null for type {Type}.", typeof(T));
            }
            else
            {
                _logger.Information("JSON deserialization completed successfully for type {Type}.", typeof(T));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during JSON deserialization for type {Type}.", typeof(T));
            throw;
        }
    }
}
