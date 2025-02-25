#region

using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Files.Converters;
using DropBear.Codex.Files.Errors;
using DropBear.Codex.Files.Models;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Extensions;

/// <summary>
///     Provides extension methods for serializing and deserializing <see cref="DropBearFile" /> objects to/from streams.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DropBearFileExtensions
{
    // Define the JSON serializer options with custom converters
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new TypeConverter(), new ContentContainerConverter(), new JsonStringEnumConverter() }
    };

    // A shared memory stream manager to optimize allocations
    private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new(
        new RecyclableMemoryStreamManager.Options
        {
            ThrowExceptionOnToArray = true,
            BlockSize = 4096 * 4, // 16KB blocks
            LargeBufferMultiple = 1024 * 1024, // 1MB increments for large buffers
            MaximumBufferSize = 1024 * 1024 * 100 // 100MB max buffer size
        });

    /// <summary>
    ///     Serializes a <see cref="DropBearFile" /> to a <see cref="MemoryStream" />.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to serialize.</param>
    /// <param name="logger">An <see cref="ILogger" /> for logging serialization details.</param>
    /// <returns>A <see cref="Result{MemoryStream, FileOperationError}" /> containing the serialized JSON data or an error.</returns>
    public static Result<MemoryStream, FileOperationError> ToStream(this DropBearFile file, ILogger logger)
    {
        if (file is null)
        {
            return Result<MemoryStream, FileOperationError>.Failure(
                FileOperationError.InvalidOperation("Cannot serialize a null DropBearFile object."));
        }

        try
        {
            var jsonString = JsonSerializer.Serialize(file, Options);
            logger.Information("Serialized DropBearFile to JSON string.");

            // Use UTF8 encoding directly to avoid intermediate string
            var memoryStream = MemoryStreamManager.GetStream();
            var bytes = Encoding.UTF8.GetBytes(jsonString);
            memoryStream.Write(bytes, 0, bytes.Length);
            memoryStream.Position = 0;

            return Result<MemoryStream, FileOperationError>.Success(memoryStream);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Serialization to stream failed.");
            return Result<MemoryStream, FileOperationError>.Failure(
                FileOperationError.SerializationFailed(ex.Message), ex);
        }
    }

    /// <summary>
    ///     Asynchronously serializes a <see cref="DropBearFile" /> to a <see cref="MemoryStream" />.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to serialize.</param>
    /// <param name="logger">An <see cref="ILogger" /> for logging serialization details.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> that can be used to cancel the operation.
    /// </param>
    /// <returns>
    ///     A <see cref="Result{MemoryStream, FileOperationError}" /> containing the serialized JSON data or an error.
    /// </returns>
    public static async Task<Result<MemoryStream, FileOperationError>> ToStreamAsync(
        this DropBearFile file,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            return Result<MemoryStream, FileOperationError>.Failure(
                FileOperationError.InvalidOperation("Cannot serialize a null DropBearFile object."));
        }

        var stream = MemoryStreamManager.GetStream();
        try
        {
            await JsonSerializer.SerializeAsync(stream, file, Options, cancellationToken).ConfigureAwait(false);
            stream.Position = 0; // Reset position after writing
            logger.Information("Asynchronously serialized DropBearFile to stream.");
            return Result<MemoryStream, FileOperationError>.Success(stream);
        }
        catch (OperationCanceledException)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            logger.Information("Serialization operation was canceled");
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            logger.Error(ex, "Serialization to stream failed.");
            return Result<MemoryStream, FileOperationError>.Failure(
                FileOperationError.SerializationFailed(ex.Message), ex);
        }
    }

    /// <summary>
    ///     Deserializes a <see cref="DropBearFile" /> from a <see cref="Stream" />.
    /// </summary>
    /// <param name="stream">A <see cref="Stream" /> containing serialized JSON data.</param>
    /// <param name="logger">An <see cref="ILogger" /> for logging deserialization details.</param>
    /// <returns>A <see cref="Result{DropBearFile, FileOperationError}" /> containing the deserialized object or an error.</returns>
    public static Result<DropBearFile, FileOperationError> FromStream(Stream stream, ILogger logger)
    {
        if (stream is null)
        {
            return Result<DropBearFile, FileOperationError>.Failure(
                FileOperationError.InvalidOperation("Cannot deserialize from a null stream."));
        }

        if (!stream.CanRead)
        {
            return Result<DropBearFile, FileOperationError>.Failure(
                FileOperationError.InvalidOperation("Stream must be readable."));
        }

        try
        {
            stream.Position = 0; // Ensure reading from the start

            // Deserialize directly from stream without intermediate string
            var file = JsonSerializer.Deserialize<DropBearFile>(stream, Options);

            if (file is null)
            {
                return Result<DropBearFile, FileOperationError>.Failure(
                    FileOperationError.InvalidOperation("Deserialization resulted in a null object."));
            }

            logger.Information("Deserialized DropBearFile from stream.");
            return Result<DropBearFile, FileOperationError>.Success(file);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Deserialization from stream failed.");
            return Result<DropBearFile, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Deserialization failed: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Asynchronously deserializes a <see cref="DropBearFile" /> from a <see cref="Stream" />.
    /// </summary>
    /// <param name="stream">A <see cref="Stream" /> containing serialized JSON data.</param>
    /// <param name="logger">An <see cref="ILogger" /> for logging deserialization details.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> that can be used to cancel the operation.
    /// </param>
    /// <returns>
    ///     A <see cref="Result{DropBearFile, FileOperationError}" /> containing the deserialized object or an error.
    /// </returns>
    public static async Task<Result<DropBearFile, FileOperationError>> FromStreamAsync(
        Stream stream,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (stream is null)
        {
            return Result<DropBearFile, FileOperationError>.Failure(
                FileOperationError.InvalidOperation("Cannot deserialize from a null stream."));
        }

        if (!stream.CanRead)
        {
            return Result<DropBearFile, FileOperationError>.Failure(
                FileOperationError.InvalidOperation("Stream must be readable."));
        }

        try
        {
            stream.Position = 0; // Ensure reading from the start
            var file = await JsonSerializer
                .DeserializeAsync<DropBearFile>(stream, Options, cancellationToken)
                .ConfigureAwait(false);

            if (file is null)
            {
                return Result<DropBearFile, FileOperationError>.Failure(
                    FileOperationError.InvalidOperation("Deserialization resulted in a null object."));
            }

            logger.Information("Asynchronously deserialized DropBearFile from stream.");
            return Result<DropBearFile, FileOperationError>.Success(file);
        }
        catch (OperationCanceledException)
        {
            logger.Information("Deserialization operation was canceled");
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Deserialization from stream failed.");
            return Result<DropBearFile, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Deserialization failed: {ex.Message}"), ex);
        }
    }
}
