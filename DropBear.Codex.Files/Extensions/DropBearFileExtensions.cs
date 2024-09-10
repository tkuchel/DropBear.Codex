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
///     Provides extension methods for serializing and deserializing <see cref="DropBearFile" /> objects to and from
///     streams.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DropBearFileExtensions
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new TypeConverter(), new ContentContainerConverter() }, WriteIndented = true
    };

    private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();

    /// <summary>
    ///     Serializes a <see cref="DropBearFile" /> object to a memory stream.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> object to serialize.</param>
    /// <param name="logger">The logger for logging serialization details.</param>
    /// <returns>A memory stream containing the serialized data.</returns>
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
            logger.Error(ex, "Serialization failed.");
            throw new InvalidOperationException("Serialization failed.", ex);
        }
    }

    /// <summary>
    ///     Asynchronously serializes a <see cref="DropBearFile" /> object to a memory stream.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> object to serialize.</param>
    /// <param name="logger">The logger for logging serialization details.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains a memory stream with the serialized
    ///     data.
    /// </returns>
    public static async Task<MemoryStream> ToStreamAsync(this DropBearFile file, ILogger logger,
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
            logger.Error(ex, "Serialization failed.");
            throw new InvalidOperationException("Serialization failed.", ex);
        }
    }

    /// <summary>
    ///     Deserializes a <see cref="DropBearFile" /> object from a stream.
    /// </summary>
    /// <param name="stream">The stream containing the serialized data.</param>
    /// <param name="logger">The logger for logging deserialization details.</param>
    /// <returns>The deserialized <see cref="DropBearFile" /> object.</returns>
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
            stream.Position = 0; // Reset the position to ensure correct reading from start
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
            logger.Error(ex, "Deserialization failed.");
            throw new InvalidOperationException("Deserialization failed.", ex);
        }
    }

    /// <summary>
    ///     Asynchronously deserializes a <see cref="DropBearFile" /> object from a stream.
    /// </summary>
    /// <param name="stream">The stream containing the serialized data.</param>
    /// <param name="logger">The logger for logging deserialization details.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains the deserialized
    ///     <see cref="DropBearFile" /> object.
    /// </returns>
    public static async Task<DropBearFile> FromStreamAsync(Stream stream, ILogger logger,
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
            stream.Position = 0; // Ensure stream is at the beginning
            var file = await JsonSerializer.DeserializeAsync<DropBearFile>(stream, Options, cancellationToken)
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
            logger.Error(ex, "Deserialization failed.");
            throw new InvalidOperationException("Deserialization failed.", ex);
        }
    }
}
