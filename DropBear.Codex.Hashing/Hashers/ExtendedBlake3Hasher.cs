#region

using Blake3;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     Provides extended methods for Blake3 hashing (incremental, MAC generation, key derivation, stream hashing).
///     Inherits from <see cref="Blake3Hasher" /> but adds static helper methods.
/// </summary>
public sealed class ExtendedBlake3Hasher : Blake3Hasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ExtendedBlake3Hasher>();

    /// <summary>
    ///     Performs an incremental Blake3 hash over multiple data segments.
    /// </summary>
    /// <param name="dataSegments">An enumerable of byte arrays to be hashed in sequence.</param>
    /// <returns>The final Blake3 hash as a hex string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="dataSegments" /> is null or any segment is null.</exception>
    public static string IncrementalHash(IEnumerable<byte[]> dataSegments)
    {
        if (dataSegments is null)
        {
            Logger.Error("Data segments cannot be null for incremental hashing.");
            throw new ArgumentNullException(nameof(dataSegments), "Data segments cannot be null.");
        }

        try
        {
            Logger.Information("Starting incremental Blake3 hash.");
            using var hasher = Hasher.New();
            foreach (var segment in dataSegments)
            {
                if (segment is null)
                {
                    Logger.Error("Data segment cannot be null.");
                    throw new ArgumentNullException(nameof(dataSegments), "Data segment cannot be null.");
                }

                hasher.Update(segment);
            }

            var result = hasher.Finalize().ToString();
            Logger.Information("Incremental hash completed successfully.");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during incremental Blake3 hash.");
            throw;
        }
    }

    /// <summary>
    ///     Generates a Blake3-based MAC (Message Authentication Code) using a 32-byte key.
    /// </summary>
    /// <param name="data">The data to MAC.</param>
    /// <param name="key">A 32-byte key used for MAC generation.</param>
    /// <returns>A hex string representing the MAC.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key" /> is not 32 bytes in length.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="data" /> is null.</exception>
    public static string GenerateMac(byte[] data, byte[] key)
    {
        if (key is null || key.Length != 32)
        {
            Logger.Error("Key must be 256 bits (32 bytes) for Blake3 MAC generation.");
            throw new ArgumentException("Key must be 256 bits (32 bytes).", nameof(key));
        }

        if (data is null)
        {
            Logger.Error("Data cannot be null for MAC generation.");
            throw new ArgumentNullException(nameof(data), "Data cannot be null.");
        }

        try
        {
            Logger.Information("Generating MAC with Blake3 keyed hasher.");
            using var hasher = Hasher.NewKeyed(key);
            hasher.Update(data);

            var result = hasher.Finalize().ToString();
            Logger.Information("MAC generated successfully using Blake3.");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 MAC generation.");
            throw;
        }
    }

    /// <summary>
    ///     Derives a new key from input keying material using Blake3.
    /// </summary>
    /// <param name="context">A byte array representing the context string for domain separation.</param>
    /// <param name="inputKeyingMaterial">The input keying material from which to derive a key.</param>
    /// <returns>A newly derived key as a byte array.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="context" /> or
    ///     <paramref name="inputKeyingMaterial" /> is null.
    /// </exception>
    public static byte[] DeriveKey(byte[] context, byte[] inputKeyingMaterial)
    {
        if (context is null)
        {
            Logger.Error("Context cannot be null for key derivation.");
            throw new ArgumentNullException(nameof(context), "Context cannot be null.");
        }

        if (inputKeyingMaterial is null)
        {
            Logger.Error("Input keying material cannot be null for key derivation.");
            throw new ArgumentNullException(nameof(inputKeyingMaterial), "Input keying material cannot be null.");
        }

        try
        {
            Logger.Information("Deriving key with Blake3 derive-key functionality.");
            using var hasher = Hasher.NewDeriveKey(context);
            hasher.Update(inputKeyingMaterial);

            var result = hasher.Finalize().AsSpan().ToArray();
            Logger.Information("Key derived successfully with Blake3.");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 key derivation.");
            throw;
        }
    }

    /// <summary>
    ///     Hashes the contents of the provided <see cref="Stream" /> using Blake3 in a streaming fashion.
    /// </summary>
    /// <param name="inputStream">The input stream to read and hash.</param>
    /// <returns>The final hash as a hex string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="inputStream" /> is null.</exception>
    public static string HashStream(Stream inputStream)
    {
        if (inputStream is null)
        {
            Logger.Error("Input stream cannot be null for Blake3 stream hashing.");
            throw new ArgumentNullException(nameof(inputStream), "Input stream cannot be null.");
        }

        try
        {
            Logger.Information("Hashing stream with Blake3.");
            using var hasher = Hasher.New();
            var buffer = new byte[4096];

            int bytesRead;
            while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hasher.Update(buffer.AsSpan(0, bytesRead));
            }

            var result = hasher.Finalize().ToString();
            Logger.Information("Stream hashing completed successfully with Blake3.");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 stream hashing.");
            throw;
        }
    }
}
