#region

using System.Diagnostics;
using System.Text.Json;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Writers;

/// <summary>
///     Implementation of <see cref="ISerializerReader" /> for JSON serialization.
/// </summary>
public sealed class JsonSerializerReader : ISerializerReader
{
    private readonly int _bufferSize;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<JsonSerializerReader>();
    private readonly JsonSerializerOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonSerializerReader" /> class with the specified options.
    /// </summary>
    /// <param name="options">The JSON serializer options.</param>
    /// <param name="bufferSize">The buffer size for stream operations.</param>
    /// <exception cref="ArgumentNullException">Thrown if options is null.</exception>
    public JsonSerializerReader(JsonSerializerOptions options, int bufferSize = 81920)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _bufferSize = bufferSize > 0 ? bufferSize : 81920; // Default to 80KB if invalid

        _logger.Information("JsonSerializerReader initialized with BufferSize: {BufferSize}, " +
                            "WriteIndented: {WriteIndented}, MaxDepth: {MaxDepth}",
            _bufferSize, _options.WriteIndented, _options.MaxDepth);
    }

    /// <summary>
    ///     Deserializes data from the stream into the specified type using JSON format.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="stream">The stream to read the serialized data from.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A result containing the deserialized value on success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="JsonException">Thrown when JSON deserialization fails.</exception>
    public async Task<Result<T, SerializationError>> DeserializeAsync<T>(Stream stream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate stream
            ArgumentNullException.ThrowIfNull(stream, nameof(stream));

            if (!stream.CanRead)
            {
                return Result<T, SerializationError>.Failure(
                    SerializationError.ForType<T>("Stream does not support reading", "StreamDeserialize"));
            }

            _logger.Information("Starting JSON deserialization from stream for type {Type}.", typeof(T));
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken)
                    .ConfigureAwait(false);

                stopwatch.Stop();

                if (result == null && typeof(T).IsClass)
                {
                    _logger.Warning("JSON deserialization resulted in null for non-nullable type {Type}.", typeof(T));

                    return Result<T, SerializationError>.Failure(
                        SerializationError.ForType<T>("Deserialization resulted in null for non-nullable type",
                            "StreamDeserialize"));
                }

                _logger.Information(
                    "JSON deserialization from stream completed successfully for type {Type} in {ElapsedMs}ms.",
                    typeof(T).Name, stopwatch.ElapsedMilliseconds);

                return Result<T, SerializationError>.Success(result!);
            }
            catch (JsonException ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "JSON deserialization error for type {Type}: {Message}", typeof(T).Name, ex.Message);
                return Result<T, SerializationError>.Failure(
                    SerializationError.ForType<T>($"JSON deserialization failed: {ex.Message}", "StreamDeserialize"),
                    ex);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during JSON stream deserialization for type {Type}: {Message}",
                typeof(T).Name, ex.Message);

            return Result<T, SerializationError>.Failure(
                SerializationError.ForType<T>($"Stream deserialization error: {ex.Message}", "StreamDeserialize"),
                ex);
        }
    }

    /// <summary>
    ///     Gets information about the reader configuration.
    /// </summary>
    /// <returns>A dictionary containing reader configuration information.</returns>
    public IDictionary<string, object> GetReaderOptions()
    {
        return new Dictionary<string, object>
(StringComparer.Ordinal)
        {
            ["ReaderType"] = "JsonSerializerReader",
            ["WriteIndented"] = _options.WriteIndented,
            ["PropertyNameCaseInsensitive"] = _options.PropertyNameCaseInsensitive,
            ["MaxDepth"] = _options.MaxDepth,
            ["BufferSize"] = _bufferSize,
            ["PropertyNamingPolicy"] = _options.PropertyNamingPolicy?.GetType().Name ?? "null"
        };
    }
}
