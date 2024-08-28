#region

using Blake3;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

public class ExtendedBlake3Hasher : Blake3Hasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ExtendedBlake3Hasher>();

    public static string IncrementalHash(IEnumerable<byte[]> dataSegments)
    {
        if (dataSegments is null)
        {
            Logger.Error("Data segments cannot be null.");
            throw new ArgumentNullException(nameof(dataSegments), "Data segments cannot be null.");
        }

        try
        {
            Logger.Information("Starting incremental hash.");
            using var hasher = Hasher.New();
            foreach (var segment in dataSegments)
            {
                if (segment is null)
                {
                    Logger.Error("Data segment cannot be null.");
#pragma warning disable MA0015
                    throw new ArgumentNullException(nameof(segment), "Data segment cannot be null.");
#pragma warning restore MA0015
                }

                hasher.Update(segment);
            }

            var result = hasher.Finalize().ToString();
            Logger.Information("Incremental hash completed successfully.");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during incremental hash.");
            throw;
        }
    }

    public static string GenerateMac(byte[] data, byte[] key)
    {
        if (key.Length is not 32)
        {
            Logger.Error("Key must be 256 bits (32 bytes).");
            throw new ArgumentException("Key must be 256 bits (32 bytes).", nameof(key));
        }

        if (data is null)
        {
            Logger.Error("Data cannot be null.");
            throw new ArgumentNullException(nameof(data), "Data cannot be null.");
        }

        try
        {
            Logger.Information("Generating MAC.");
            using var hasher = Hasher.NewKeyed(key);
            hasher.Update(data);
            var result = hasher.Finalize().ToString();
            Logger.Information("MAC generated successfully.");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during MAC generation.");
            throw;
        }
    }

    public static byte[] DeriveKey(byte[] context, byte[] inputKeyingMaterial)
    {
        if (context is null)
        {
            Logger.Error("Context cannot be null.");
            throw new ArgumentNullException(nameof(context), "Context cannot be null.");
        }

        if (inputKeyingMaterial is null)
        {
            Logger.Error("Input keying material cannot be null.");
            throw new ArgumentNullException(nameof(inputKeyingMaterial), "Input keying material cannot be null.");
        }

        try
        {
            Logger.Information("Deriving key.");
            using var hasher = Hasher.NewDeriveKey(context);
            hasher.Update(inputKeyingMaterial);

            var result = hasher.Finalize().AsSpan().ToArray();
            Logger.Information("Key derived successfully.");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during key derivation.");
            throw;
        }
    }

    public static string HashStream(Stream inputStream)
    {
        if (inputStream is null)
        {
            Logger.Error("Input stream cannot be null.");
            throw new ArgumentNullException(nameof(inputStream), "Input stream cannot be null.");
        }

        try
        {
            Logger.Information("Hashing stream.");
            using var hasher = Hasher.New();
            var buffer = new byte[4096]; // Buffer size can be adjusted based on needs.
            int bytesRead;
            while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hasher.Update(buffer.AsSpan(0, bytesRead));
            }

            var result = hasher.Finalize().ToString();
            Logger.Information("Stream hash completed successfully.");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during stream hashing.");
            throw;
        }
    }
}
