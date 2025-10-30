#region

using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Adapter that allows an IStreamSerializer to be used where an ISerializer is expected.
/// </summary>
public sealed class StreamSerializerAdapter : ISerializer
{
    private readonly bool _disposeStreams;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<StreamSerializerAdapter>();
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;
    private readonly IStreamSerializer _streamSerializer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamSerializerAdapter" /> class.
    /// </summary>
    /// <param name="streamSerializer">The stream serializer to adapt.</param>
    /// <param name="memoryStreamManager">The memory stream manager to use.</param>
    /// <param name="disposeStreams">Whether to dispose streams after operations.</param>
    public StreamSerializerAdapter(
        IStreamSerializer streamSerializer,
        RecyclableMemoryStreamManager? memoryStreamManager = null,
        bool disposeStreams = true)
    {
        _streamSerializer = streamSerializer ?? throw new ArgumentNullException(nameof(streamSerializer));
        _memoryStreamManager = memoryStreamManager ?? new RecyclableMemoryStreamManager();
        _disposeStreams = disposeStreams;

        _logger.Information("StreamSerializerAdapter initialized with StreamSerializer: {StreamSerializerType}, " +
                            "DisposeStreams: {DisposeStreams}",
            _streamSerializer.GetType().Name, _disposeStreams);
    }

    /// <inheritdoc />
    public async Task<Result<byte[], SerializationError>> SerializeAsync<T>(T value,
        CancellationToken cancellationToken = default)
    {
        if (value == null)
        {
            _logger.Warning("Attempted to serialize null value of type {Type}", typeof(T).Name);
            return Result<byte[], SerializationError>.Success([]);
        }

        _logger.Information("Starting stream-based serialization for type {Type}.", typeof(T).Name);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var memoryStream = _memoryStreamManager.GetStream("StreamSerializerAdapter-Serialize");

            // Serialize to the stream
            var serializeResult = await _streamSerializer.SerializeAsync(memoryStream, value, cancellationToken)
                .ConfigureAwait(false);

            if (!serializeResult.IsSuccess)
            {
                return Result<byte[], SerializationError>.Failure(serializeResult.Error!);
            }

            // Convert the stream to a byte array
            var resultBytes = memoryStream.ToArray();

            stopwatch.Stop();
            _logger.Information("Stream-based serialization completed successfully for type {Type} in {ElapsedMs}ms. " +
                                "Result size: {ResultSize} bytes",
                typeof(T).Name, stopwatch.ElapsedMilliseconds, resultBytes.Length);

            return Result<byte[], SerializationError>.Success(resultBytes);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Error occurred during stream-based serialization for type {Type}: {Message}",
                typeof(T).Name, ex.Message);

            return Result<byte[], SerializationError>.Failure(
                SerializationError.ForType<T>($"Stream serialization error: {ex.Message}", "StreamSerialize"),
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<T, SerializationError>> DeserializeAsync<T>(byte[] data,
        CancellationToken cancellationToken = default)
    {
        if (data == null || data.Length == 0)
        {
            _logger.Warning("Attempted to deserialize null or empty data to type {Type}", typeof(T).Name);
            return Result<T, SerializationError>.Failure(
                SerializationError.ForType<T>("Cannot deserialize null or empty data", "StreamDeserialize"));
        }

        _logger.Information("Starting stream-based deserialization for type {Type}.", typeof(T).Name);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Create a memory stream from the byte array
            using var memoryStream = _memoryStreamManager.GetStream("StreamSerializerAdapter-Deserialize", data);

            // Deserialize from the stream
            var deserializeResult = await _streamSerializer.DeserializeAsync<T>(memoryStream, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            if (deserializeResult.IsSuccess)
            {
                _logger.Information(
                    "Stream-based deserialization completed successfully for type {Type} in {ElapsedMs}ms.",
                    typeof(T).Name, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.Warning("Stream-based deserialization failed for type {Type} in {ElapsedMs}ms: {Error}",
                    typeof(T).Name, stopwatch.ElapsedMilliseconds, deserializeResult.Error?.Message);
            }

            return deserializeResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Error occurred during stream-based deserialization for type {Type}: {Message}",
                typeof(T).Name, ex.Message);

            return Result<T, SerializationError>.Failure(
                SerializationError.ForType<T>($"Stream deserialization error: {ex.Message}", "StreamDeserialize"),
                ex);
        }
    }

    /// <inheritdoc />
    public Dictionary<string, object> GetCapabilities()
    {
        return new Dictionary<string, object>
(StringComparer.Ordinal)
        {
            ["SerializerType"] = "StreamSerializerAdapter",
            ["InnerSerializerType"] = _streamSerializer.GetType().Name,
            ["SupportsStreaming"] = true,
            ["DisposeStreams"] = _disposeStreams
        };
    }
}
