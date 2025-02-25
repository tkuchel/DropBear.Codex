#region

using System.Buffers;
using System.Security.Cryptography;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Helpers;

/// <summary>
///     Provides helper methods for hashing-related operations, such as salt generation and
///     combining/extracting byte arrays.
/// </summary>
public static class HashingHelper
{
    // Buffer size for efficient byte array operations
    private const int DefaultBufferSize = 16384; // 16KB
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(HashingHelper));

    /// <summary>
    ///     Generates a random salt of the specified size in bytes.
    /// </summary>
    /// <param name="saltSize">The size of the salt to generate, in bytes.</param>
    /// <returns>A newly generated salt as a byte array.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="saltSize" /> is less than 1.</exception>
    public static byte[] GenerateRandomSalt(int saltSize)
    {
        if (saltSize < 1)
        {
            Logger.Error("Salt size must be at least 1 byte. Requested: {SaltSize}", saltSize);
            throw new ArgumentOutOfRangeException(nameof(saltSize), "Salt size must be at least 1.");
        }

        Logger.Information("Generating a random salt of size {SaltSize} bytes.", saltSize);

        var buffer = new byte[saltSize];
        try
        {
            RandomNumberGenerator.Fill(buffer); // .NET 6+ API, more efficient
            Logger.Debug("Random salt generated successfully: {SaltSize} bytes.", saltSize);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while generating random salt.");
            throw;
        }

        return buffer;
    }

    /// <summary>
    ///     Combines two byte arrays (salt + hash) into a single array.
    /// </summary>
    /// <param name="salt">The salt byte array. Cannot be null.</param>
    /// <param name="hash">The hash byte array. Cannot be null.</param>
    /// <returns>The combined byte array, with salt first, then hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="salt" /> or <paramref name="hash" /> is null.</exception>
    public static byte[] CombineBytes(byte[] salt, byte[] hash)
    {
        ArgumentNullException.ThrowIfNull(salt, nameof(salt));
        ArgumentNullException.ThrowIfNull(hash, nameof(hash));

        Logger.Information(
            "Combining salt and hash arrays. Salt size: {SaltSize} bytes, Hash size: {HashSize} bytes.",
            salt.Length, hash.Length);

        try
        {
            var combinedBytes = new byte[salt.Length + hash.Length];
            salt.CopyTo(combinedBytes, 0);
            hash.CopyTo(combinedBytes, salt.Length);

            Logger.Debug("Salt and hash combined successfully.");
            return combinedBytes;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while combining salt and hash arrays.");
            throw;
        }
    }

    /// <summary>
    ///     Combines two byte spans (salt + hash) into a single array efficiently.
    /// </summary>
    /// <param name="salt">The salt data.</param>
    /// <param name="hash">The hash data.</param>
    /// <returns>The combined byte array, with salt first, then hash.</returns>
    public static byte[] CombineBytes(ReadOnlySpan<byte> salt, ReadOnlySpan<byte> hash)
    {
        Logger.Information(
            "Combining salt and hash arrays. Salt size: {SaltSize} bytes, Hash size: {HashSize} bytes.",
            salt.Length, hash.Length);

        try
        {
            var combinedBytes = new byte[salt.Length + hash.Length];
            salt.CopyTo(combinedBytes);
            hash.CopyTo(combinedBytes.AsSpan(salt.Length));

            Logger.Debug("Salt and hash combined successfully.");
            return combinedBytes;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while combining salt and hash arrays.");
            throw;
        }
    }

    /// <summary>
    ///     Extracts salt and hash bytes from a combined byte array, assuming the salt is at the start.
    /// </summary>
    /// <param name="combinedBytes">The combined byte array containing salt + hash.</param>
    /// <param name="saltSize">The size (in bytes) of the salt portion.</param>
    /// <returns>
    ///     A tuple <c>(salt, hash)</c>, where <c>salt</c> is <paramref name="saltSize" /> bytes,
    ///     and <c>hash</c> is the remainder.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="combinedBytes" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown if <paramref name="saltSize" /> is invalid relative to <paramref name="combinedBytes" />.
    /// </exception>
    public static (byte[] salt, byte[] hash) ExtractBytes(byte[] combinedBytes, int saltSize)
    {
        ArgumentNullException.ThrowIfNull(combinedBytes, nameof(combinedBytes));
        if (saltSize < 0 || saltSize > combinedBytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(saltSize), "Salt size is invalid for the combined array.");
        }

        Logger.Information("Extracting salt and hash from combined byte array. Salt size: {SaltSize} bytes.", saltSize);

        try
        {
            var salt = new byte[saltSize];
            var hash = new byte[combinedBytes.Length - saltSize];

            combinedBytes.AsSpan(0, saltSize).CopyTo(salt);
            combinedBytes.AsSpan(saltSize).CopyTo(hash);

            Logger.Debug("Salt and hash extracted successfully.");
            return (salt, hash);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while extracting salt and hash from combined byte array.");
            throw;
        }
    }

    /// <summary>
    ///     Extracts salt and hash bytes from a combined ReadOnlyMemory buffer, assuming the salt is at the start.
    /// </summary>
    /// <param name="combinedBytes">The combined bytes containing salt + hash.</param>
    /// <param name="saltSize">The size (in bytes) of the salt portion.</param>
    /// <returns>
    ///     A tuple <c>(salt, hash)</c>, where <c>salt</c> is <paramref name="saltSize" /> bytes,
    ///     and <c>hash</c> is the remainder.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown if <paramref name="saltSize" /> is invalid relative to <paramref name="combinedBytes" />.
    /// </exception>
    public static (byte[] salt, byte[] hash) ExtractBytes(ReadOnlyMemory<byte> combinedBytes, int saltSize)
    {
        if (saltSize < 0 || saltSize > combinedBytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(saltSize), "Salt size is invalid for the combined array.");
        }

        Logger.Information("Extracting salt and hash from combined byte array. Salt size: {SaltSize} bytes.", saltSize);

        try
        {
            var salt = combinedBytes.Slice(0, saltSize).ToArray();
            var hash = combinedBytes.Slice(saltSize).ToArray();

            Logger.Debug("Salt and hash extracted successfully.");
            return (salt, hash);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while extracting salt and hash from combined byte array.");
            throw;
        }
    }

    /// <summary>
    ///     Converts a byte array to a Base64-encoded string.
    /// </summary>
    /// <param name="byteArray">The byte array to convert. Cannot be null.</param>
    /// <returns>A Base64-encoded string representing <paramref name="byteArray" />.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="byteArray" /> is null.</exception>
    public static string ConvertByteArrayToBase64String(byte[] byteArray)
    {
        ArgumentNullException.ThrowIfNull(byteArray, nameof(byteArray));

        Logger.Information("Converting byte array to base64 string. Byte array size: {ByteArraySize} bytes.",
            byteArray.Length);

        try
        {
            var base64String = Convert.ToBase64String(byteArray);
            Logger.Debug("Byte array converted to base64 string successfully.");
            return base64String;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while converting byte array to base64 string.");
            throw;
        }
    }

    /// <summary>
    ///     Converts a ReadOnlySpan of bytes to a Base64-encoded string.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <returns>A Base64-encoded string representing <paramref name="bytes" />.</returns>
    public static string ConvertByteArrayToBase64String(ReadOnlySpan<byte> bytes)
    {
        Logger.Information("Converting byte span to base64 string. Byte span size: {ByteArraySize} bytes.",
            bytes.Length);

        try
        {
            var base64String = Convert.ToBase64String(bytes);
            Logger.Debug("Byte span converted to base64 string successfully.");
            return base64String;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while converting byte span to base64 string.");
            throw;
        }
    }

    /// <summary>
    ///     Converts a Base64-encoded string to a byte array.
    /// </summary>
    /// <param name="str">The Base64-encoded string. Cannot be null.</param>
    /// <returns>A byte array decoded from the Base64 string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="str" /> is null.</exception>
    public static byte[] ConvertBase64StringToByteArray(string str)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        Logger.Information("Converting base64 string to byte array. String length: {StringLength} characters.",
            str.Length);

        try
        {
            var byteArray = Convert.FromBase64String(str);
            Logger.Debug("Base64 string converted to byte array successfully.");
            return byteArray;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while converting base64 string to byte array.");
            throw;
        }
    }

    /// <summary>
    ///     Converts a segment of a byte array to Base64 string without copying the array.
    /// </summary>
    /// <param name="data">The source byte array.</param>
    /// <param name="offset">Starting position in the array.</param>
    /// <param name="length">Number of bytes to convert.</param>
    /// <returns>A Base64-encoded string.</returns>
    public static string ByteSegmentToBase64(byte[] data, int offset, int length)
    {
        ArgumentNullException.ThrowIfNull(data, nameof(data));

        if (offset < 0 || offset >= data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset is outside the array bounds.");
        }

        if (length < 0 || offset + length > data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length is invalid for the given offset and array.");
        }

        Logger.Information(
            "Converting byte segment to base64. Offset: {Offset}, Length: {Length}, Total Size: {Size}",
            offset, length, data.Length);

        try
        {
            var base64 = Convert.ToBase64String(data, offset, length);
            Logger.Debug("Byte segment converted to base64 successfully.");
            return base64;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while converting byte segment to base64.");
            throw;
        }
    }

    /// <summary>
    ///     Asynchronously hashes a large stream using chunked processing.
    /// </summary>
    /// <param name="stream">The stream to hash.</param>
    /// <param name="hasher">A function that incrementally updates the hash with each chunk.</param>
    /// <param name="finalizer">A function that finalizes and returns the hash result.</param>
    /// <param name="bufferSize">Size of buffer to use for reading (defaults to 16KB).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resulting hash as a byte array.</returns>
    public static async Task<byte[]> HashStreamChunkedAsync(
        Stream stream,
        Action<byte[], int, int> hasher,
        Func<byte[]> finalizer,
        int bufferSize = DefaultBufferSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream, nameof(stream));
        ArgumentNullException.ThrowIfNull(hasher, nameof(hasher));
        ArgumentNullException.ThrowIfNull(finalizer, nameof(finalizer));

        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be positive.");
        }

        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        Logger.Information("Beginning chunked stream hashing. Buffer size: {BufferSize} bytes", bufferSize);

        // Use ArrayPool for efficient buffer management
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                       .ConfigureAwait(false)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hasher(buffer, 0, bytesRead);
            }

            var result = finalizer();
            Logger.Information("Chunked stream hashing completed. Result size: {ResultSize} bytes", result.Length);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
