#region

using System.Diagnostics;
using System.Text.Json;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     A serializer that handles both serialization and deserialization of JSON data using streams.
/// </summary>
public sealed class JsonStreamSerializer : IStreamSerializer
{
    private readonly int _bufferSize;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<JsonStreamSerializer>();
    private readonly JsonSerializerOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonStreamSerializer" /> class with the specified options.
    /// </summary>
    /// <param name="options">The JSON serializer options.</param>
    /// <param name="bufferSize">The buffer size for stream operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public JsonStreamSerializer(JsonSerializerOptions options, int bufferSize = 81920)
    {
        _options = options ??
                   throw new ArgumentNullException(nameof(options), "JSON serializer options cannot be null.");
        _bufferSize = bufferSize > 0 ? bufferSize : 81920; // Default to 80KB if invalid

        _logger.Information("JsonStreamSerializer initialized with BufferSize: {BufferSize}, " +
                            "WriteIndented: {WriteIndented}, MaxDepth: {MaxDepth}",
            _bufferSize, _options.WriteIndented, _options.MaxDepth);
    }

    /// <inheritdoc />
    public bool CanHandleType(Type type)
    {
        // JSON can handle most types except those that are specifically problematic
        if (type == typeof(Stream) ||
            type == typeof(byte[]) ||
            type == typeof(Memory<byte>) ||
            type == typeof(ReadOnlyMemory<byte>))
        {
            return false;
        }

        // Check if the type has a custom converter registered
        return _options.GetConverter(type) != null ||
               (!type.IsPointer && !type.IsByRef && !type.IsUnsafe());
    }

    /// <inheritdoc />
    public async Task<Result<Unit, SerializationError>> SerializeAsync<T>(
        Stream stream,
        T value,
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

            _logger.Information("Starting JSON stream serialization for type {Type}.", typeof(T).Name);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await System.Text.Json.JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken)
                    .ConfigureAwait(false);

                stopwatch.Stop();
                _logger.Information(
                    "JSON stream serialization completed successfully for type {Type} in {ElapsedMs}ms.",
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
    public async Task<Result<T, SerializationError>> DeserializeAsync<T>(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate parameters
            ArgumentNullException.ThrowIfNull(stream, nameof(stream));

            if (!stream.CanRead)
            {
                return Result<T, SerializationError>.Failure(
                    SerializationError.ForType<T>("Stream does not support reading", "StreamDeserialize"));
            }

            _logger.Information("Starting JSON stream deserialization for type {Type}.", typeof(T).Name);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await System.Text.Json.JsonSerializer
                    .DeserializeAsync<T>(stream, _options, cancellationToken)
                    .ConfigureAwait(false);

                stopwatch.Stop();

                if (result == null && typeof(T).IsClass)
                {
                    _logger.Warning("JSON stream deserialization resulted in null for non-nullable type {Type}",
                        typeof(T).Name);

                    return Result<T, SerializationError>.Failure(
                        SerializationError.ForType<T>("Deserialization resulted in null for non-nullable type",
                            "StreamDeserialize"));
                }

                _logger.Information(
                    "JSON stream deserialization completed successfully for type {Type} in {ElapsedMs}ms.",
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
}

/// <summary>
///     Extension methods for Type to check for unsafe types.
/// </summary>
internal static class TypeExtensions
{
    /// <summary>
    ///     Determines if a type is considered unsafe for serialization.
    /// </summary>
    public static bool IsUnsafe(this Type type)
    {
        return type.IsPointer ||
               type.IsByRef ||
               type == typeof(IntPtr) ||
               type == typeof(UIntPtr) ||
               type.IsFunctionPointer;
    }

    /// <summary>
    ///     Checks if the type is a function pointer (for .NET 7+).
    /// </summary>
    public static bool IsFunctionPointer(this Type type)
    {
        // In .NET 5+, there's a direct API for this
        // For .NET Standard compatibility, we check indirectly
        return type.IsPointer && type.ToString().Contains("System.Reflection.MethodInfo*");
    }
}
