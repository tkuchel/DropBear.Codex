#region

using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using DropBear.Codex.Files.Converters;
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
    private static readonly JsonSerializerOptions Options = new()
    {
        // Add our custom converters for <see cref="Type"/> and <see cref="ContentContainer"/>.
        Converters = { new TypeConverter(), new ContentContainerConverter() }, WriteIndented = true
    };

    // A shared memory stream manager to optimize allocations.
    private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();

    /// <summary>
    ///     Serializes a <see cref="DropBearFile" /> to a <see cref="MemoryStream" />.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to serialize.</param>
    /// <param name="logger">An <see cref="ILogger" /> for logging serialization details.</param>
    /// <returns>A <see cref="MemoryStream" /> containing the serialized JSON data.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="file" /> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if serialization fails.</exception>
    public static MemoryStream ToStream(this DropBearFile file, ILogger logger)
    {
        if (file is null)
        {
            throw new ArgumentNullException(nameof(file), "Cannot serialize a null DropBearFile object.");
        }

        try
        {
            var jsonString = JsonSerializer.Serialize(file, Options);
            logger.Information("Serialized DropBearFile to JSON string.");
            return MemoryStreamManager.GetStream(Encoding.UTF8.GetBytes(jsonString));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Serialization to stream failed.");
            throw new InvalidOperationException("Serialization failed.", ex);
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
    ///     A task that completes with a <see cref="MemoryStream" /> containing the serialized JSON data.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="file" /> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if serialization fails.</exception>
    public static async Task<MemoryStream> ToStreamAsync(
        this DropBearFile file,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            throw new ArgumentNullException(nameof(file), "Cannot serialize a null DropBearFile object.");
        }

        var stream = MemoryStreamManager.GetStream();
        try
        {
            await JsonSerializer.SerializeAsync(stream, file, Options, cancellationToken).ConfigureAwait(false);
            stream.Position = 0; // Reset position after writing
            logger.Information("Asynchronously serialized DropBearFile to stream.");
            return stream;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Serialization to stream failed.");
            throw new InvalidOperationException("Serialization failed.", ex);
        }
    }

    /// <summary>
    ///     Deserializes a <see cref="DropBearFile" /> from a <see cref="Stream" />.
    /// </summary>
    /// <param name="stream">A <see cref="Stream" /> containing serialized JSON data.</param>
    /// <param name="logger">An <see cref="ILogger" /> for logging deserialization details.</param>
    /// <returns>The deserialized <see cref="DropBearFile" />.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream" /> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown if <paramref name="stream" /> is not readable.</exception>
    /// <exception cref="InvalidOperationException">Thrown if deserialization fails or results in a null object.</exception>
    public static DropBearFile FromStream(Stream stream, ILogger logger)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream), "Cannot deserialize from a null stream.");
        }

        if (!stream.CanRead)
        {
            throw new NotSupportedException("Stream must be readable.");
        }

        try
        {
            stream.Position = 0; // Ensure reading from the start
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true);
            var jsonString = reader.ReadToEnd();
            var file = JsonSerializer.Deserialize<DropBearFile>(jsonString, Options);

            if (file is null)
            {
                throw new InvalidOperationException("Deserialization resulted in a null object.");
            }

            logger.Information("Deserialized DropBearFile from stream.");
            return file;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Deserialization from stream failed.");
            throw new InvalidOperationException("Deserialization failed.", ex);
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
    ///     A <see cref="Task{T}" /> representing the async operation, returning the deserialized
    ///     <see cref="DropBearFile" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream" /> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown if <paramref name="stream" /> is not readable.</exception>
    /// <exception cref="InvalidOperationException">Thrown if deserialization fails or results in a null object.</exception>
    public static async Task<DropBearFile> FromStreamAsync(
        Stream stream,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream), "Cannot deserialize from a null stream.");
        }

        if (!stream.CanRead)
        {
            throw new NotSupportedException("Stream must be readable.");
        }

        try
        {
            stream.Position = 0; // Ensure reading from the start
            var file = await JsonSerializer
                .DeserializeAsync<DropBearFile>(stream, Options, cancellationToken)
                .ConfigureAwait(false);

            if (file is null)
            {
                throw new InvalidOperationException("Deserialization resulted in a null object.");
            }

            logger.Information("Asynchronously deserialized DropBearFile from stream.");
            return file;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Deserialization from stream failed.");
            throw new InvalidOperationException("Deserialization failed.", ex);
        }
    }
}
