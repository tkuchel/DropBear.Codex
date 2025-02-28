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
///     Implementation of <see cref="ISerializerWriter" /> for JSON serialization.
/// </summary>
public sealed class JsonSerializerWriter : ISerializerWriter
{
    private readonly int _bufferSize;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<JsonSerializerWriter>();
    private readonly JsonSerializerOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonSerializerWriter" /> class with the specified options.
    /// </summary>
    /// <param name="options">The JSON serializer options.</param>
    /// <param name="bufferSize">The buffer size for stream operations.</param>
    /// <exception cref="ArgumentNullException">Thrown if options is null.</exception>
    public JsonSerializerWriter(JsonSerializerOptions options, int bufferSize = 81920)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _bufferSize = bufferSize > 0 ? bufferSize : 81920; // Default to 80KB if invalid

        _logger.Information("JsonSerializerWriter initialized with BufferSize: {BufferSize}, " +
                            "WriteIndented: {WriteIndented}, MaxDepth: {MaxDepth}",
            _bufferSize, _options.WriteIndented, _options.MaxDepth);
    }

    /// <inheritdoc />
    public async Task<Result<Unit, SerializationError>> SerializeAsync<T>(Stream stream, T value,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate parameters
            ArgumentNullException.ThrowIfNull(stream, nameof(stream));

            if (!stream.CanWrite)
            {
                return Result<Unit, SerializationError>.Failure(
                    new SerializationError("Stream does not support writing") { Operation = "StreamSerialize" });
            }

            if (value == null)
            {
                _logger.Warning("Attempted to serialize null value of type {Type}", typeof(T).Name);

                // Write an empty JSON object for null values
                await stream.WriteAsync(new[] { (byte)'{', (byte)'}' }, cancellationToken).ConfigureAwait(false);
                return Result<Unit, SerializationError>.Success(Unit.Value);
            }

            _logger.Information("Starting JSON serialization to stream for type {Type}.", typeof(T).Name);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                stopwatch.Stop();
                _logger.Information(
                    "JSON serialization to stream completed successfully for type {Type} in {ElapsedMs}ms.",
                    typeof(T).Name, stopwatch.ElapsedMilliseconds);

                return Result<Unit, SerializationError>.Success(Unit.Value);
            }
            catch (JsonException ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "JSON serialization error for type {Type}: {Message}", typeof(T).Name, ex.Message);
                return Result<Unit, SerializationError>.Failure(
                    SerializationError.ForType<T>($"JSON serialization failed: {ex.Message}", "StreamSerialize"),
                    ex);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during JSON stream serialization for type {Type}: {Message}",
                typeof(T).Name, ex.Message);

            return Result<Unit, SerializationError>.Failure(
                SerializationError.ForType<T>($"Stream serialization error: {ex.Message}", "StreamSerialize"),
                ex);
        }
    }

    /// <inheritdoc />
    public IDictionary<string, object> GetWriterOptions()
    {
        return new Dictionary<string, object>
(StringComparer.Ordinal)
        {
            ["WriterType"] = "JsonSerializerWriter",
            ["WriteIndented"] = _options.WriteIndented,
            ["PropertyNameCaseInsensitive"] = _options.PropertyNameCaseInsensitive,
            ["MaxDepth"] = _options.MaxDepth,
            ["BufferSize"] = _bufferSize,
            ["PropertyNamingPolicy"] = _options.PropertyNamingPolicy?.GetType().Name ?? "null"
        };
    }
}
