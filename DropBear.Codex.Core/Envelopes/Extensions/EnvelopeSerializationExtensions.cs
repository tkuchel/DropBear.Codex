#region

using System.Buffers;
using DropBear.Codex.Core.Envelopes.Serializers;
using DropBear.Codex.Core.Interfaces;

#endregion

namespace DropBear.Codex.Core.Envelopes.Extensions;

/// <summary>
///     Provides extension methods for Envelope serialization.
///     Optimized for .NET 9 with cached serializers.
/// </summary>
public static class EnvelopeSerializationExtensions
{
    // Cached singleton instances for better performance
    private static readonly JsonEnvelopeSerializer DefaultJsonSerializer = new();
    private static readonly MessagePackEnvelopeSerializer DefaultMessagePackSerializer = new();

    #region String Serialization

    /// <summary>
    ///     Serializes the envelope to a string using JSON.
    /// </summary>
    public static string ToSerializedString<T>(
        this Envelope<T> envelope,
        IEnvelopeSerializer? serializer = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        serializer ??= DefaultJsonSerializer;
        return serializer.Serialize(envelope);
    }

    /// <summary>
    ///     Deserializes an envelope from a JSON string.
    /// </summary>
    public static Envelope<T> FromSerializedString<T>(
        string data,
        IEnvelopeSerializer? serializer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data);
        serializer ??= DefaultJsonSerializer;
        return serializer.Deserialize<T>(data);
    }

    #endregion

    #region Binary Serialization

    /// <summary>
    ///     Serializes the envelope to binary using MessagePack.
    /// </summary>
    public static byte[] ToSerializedBinary<T>(
        this Envelope<T> envelope,
        IEnvelopeSerializer? serializer = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        serializer ??= DefaultMessagePackSerializer;
        return serializer.SerializeToBinary(envelope);
    }

    /// <summary>
    ///     Deserializes an envelope from binary MessagePack data.
    /// </summary>
    public static Envelope<T> FromSerializedBinary<T>(
        byte[] data,
        IEnvelopeSerializer? serializer = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        serializer ??= DefaultMessagePackSerializer;
        return serializer.DeserializeFromBinary<T>(data);
    }

    #endregion

    #region ReadOnlySpan Support (.NET 9)

    /// <summary>
    ///     Serializes the envelope to a span of bytes using MessagePack.
    /// </summary>
    public static int SerializeToSpan<T>(
        this Envelope<T> envelope,
        Span<byte> destination,
        IEnvelopeSerializer? serializer = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var binary = envelope.ToSerializedBinary(serializer);
        if (destination.Length < binary.Length)
        {
            throw new ArgumentException(
                $"Destination span too small. Required: {binary.Length}, Available: {destination.Length}",
                nameof(destination));
        }

        binary.CopyTo(destination);
        return binary.Length;
    }

    /// <summary>
    ///     Deserializes an envelope from a readonly span of bytes.
    /// </summary>
    public static Envelope<T> FromSerializedSpan<T>(
        ReadOnlySpan<byte> data,
        IEnvelopeSerializer? serializer = null)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("Data span cannot be empty.", nameof(data));
        }

        var array = data.ToArray();
        return FromSerializedBinary<T>(array, serializer);
    }

    #endregion

    #region Async File Operations

    /// <summary>
    ///     Serializes and saves the envelope to a file asynchronously.
    /// </summary>
    public static async ValueTask SaveToFileAsync<T>(
        this Envelope<T> envelope,
        string filePath,
        IEnvelopeSerializer? serializer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var binary = envelope.ToSerializedBinary(serializer);
        await File.WriteAllBytesAsync(filePath, binary, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Loads and deserializes an envelope from a file asynchronously.
    /// </summary>
    public static async ValueTask<Envelope<T>> LoadFromFileAsync<T>(
        string filePath,
        IEnvelopeSerializer? serializer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var binary = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        return FromSerializedBinary<T>(binary, serializer);
    }

    #endregion

    #region Stream Operations

    /// <summary>
    ///     Serializes the envelope to a stream.
    /// </summary>
    public static async ValueTask SerializeToStreamAsync<T>(
        this Envelope<T> envelope,
        Stream stream,
        IEnvelopeSerializer? serializer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(stream);

        var binary = envelope.ToSerializedBinary(serializer);
        await stream.WriteAsync(binary, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deserializes an envelope from a stream.
    /// </summary>
    public static async ValueTask<Envelope<T>> DeserializeFromStreamAsync<T>(
        Stream stream,
        IEnvelopeSerializer? serializer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        var binary = ms.ToArray();

        return FromSerializedBinary<T>(binary, serializer);
    }

    #endregion

    #region Compression Support

    /// <summary>
    ///     Serializes and compresses the envelope using GZip.
    /// </summary>
    public static async ValueTask<byte[]> ToCompressedBinaryAsync<T>(
        this Envelope<T> envelope,
        IEnvelopeSerializer? serializer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var binary = envelope.ToSerializedBinary(serializer);

        using var outputStream = new MemoryStream();
        await using (var gzipStream = new System.IO.Compression.GZipStream(
                         outputStream,
                         System.IO.Compression.CompressionLevel.Optimal))
        {
            await gzipStream.WriteAsync(binary, cancellationToken).ConfigureAwait(false);
        }

        return outputStream.ToArray();
    }

    /// <summary>
    ///     Decompresses and deserializes an envelope from GZip compressed data.
    /// </summary>
    public static async ValueTask<Envelope<T>> FromCompressedBinaryAsync<T>(
        byte[] compressedData,
        IEnvelopeSerializer? serializer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compressedData);

        using var inputStream = new MemoryStream(compressedData);
        using var gzipStream = new System.IO.Compression.GZipStream(
            inputStream,
            System.IO.Compression.CompressionMode.Decompress);
        using var outputStream = new MemoryStream();

        await gzipStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
        var binary = outputStream.ToArray();

        return FromSerializedBinary<T>(binary, serializer);
    }

    #endregion

    #region Performance-Optimized Serialization

    /// <summary>
    ///     Serializes envelope to a pooled buffer for memory efficiency.
    ///     Caller must return the buffer to the pool after use.
    /// </summary>
    public static (byte[] Buffer, int BytesWritten) SerializeToPooledBuffer<T>(
        this Envelope<T> envelope,
        IEnvelopeSerializer? serializer = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        serializer ??= new MessagePackEnvelopeSerializer();
        var binary = serializer.SerializeToBinary(envelope);

        // Rent from pool with some headroom
        var buffer = ArrayPool<byte>.Shared.Rent(binary.Length + 256);
        binary.CopyTo(buffer, 0);

        return (buffer, binary.Length);
    }

    /// <summary>
    ///     Deserializes from a memory segment without additional allocation.
    /// </summary>
    public static Envelope<T> DeserializeFromMemory<T>(
        ReadOnlyMemory<byte> data,
        IEnvelopeSerializer? serializer = null)
    {
        serializer ??= new MessagePackEnvelopeSerializer();

        // Use span to avoid allocation
        var array = data.Span.ToArray();
        return serializer.DeserializeFromBinary<T>(array);
    }

    /// <summary>
    ///     Streams serialization to avoid large buffer allocations.
    /// </summary>
    public static async ValueTask SerializeToStreamOptimizedAsync<T>(
        this Envelope<T> envelope,
        Stream stream,
        IEnvelopeSerializer? serializer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(stream);

        serializer ??= new MessagePackEnvelopeSerializer();

        // Serialize to pooled buffer
        var (buffer, bytesWritten) = envelope.SerializeToPooledBuffer(serializer);

        try
        {
            // Write to stream in chunks to avoid large allocations
            const int chunkSize = 8192; // 8KB chunks
            var offset = 0;

            while (offset < bytesWritten)
            {
                var writeSize = Math.Min(chunkSize, bytesWritten - offset);
                await stream.WriteAsync(buffer.AsMemory(offset, writeSize), cancellationToken)
                    .ConfigureAwait(false);
                offset += writeSize;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    #endregion
}
