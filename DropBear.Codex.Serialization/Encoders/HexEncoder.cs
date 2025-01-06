#region

using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Encoders;

/// <summary>
///     Provides methods to encode and decode data using hexadecimal encoding.
/// </summary>
public sealed class HexEncoder : IEncoder
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HexEncoder" /> class.
    /// </summary>
    public HexEncoder()
    {
        _logger = LoggerFactory.Logger.ForContext<HexEncoder>();
    }

    /// <inheritdoc />
    public async Task<byte[]> EncodeAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data == null)
        {
            _logger.Error("Input data cannot be null.");
            throw new ArgumentNullException(nameof(data), "Input data cannot be null.");
        }

        try
        {
            return await Task.Run(() =>
            {
                var hexString = BitConverter.ToString(data)
                    .Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);
                return Encoding.UTF8.GetBytes(hexString);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during hexadecimal encoding");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> DecodeAsync(byte[] encodedData, CancellationToken cancellationToken = default)
    {
        if (encodedData == null)
        {
            _logger.Error("Encoded data cannot be null.");
            throw new ArgumentNullException(nameof(encodedData), "Encoded data cannot be null.");
        }

        try
        {
            return await Task.Run(() =>
            {
                var hexString = Encoding.UTF8.GetString(encodedData);
                return ConvertHexStringToByteArray(hexString);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during hexadecimal decoding");
            throw;
        }
    }

    private static byte[] ConvertHexStringToByteArray(string hexString)
    {
        if (hexString == null)
        {
            throw new ArgumentNullException(nameof(hexString), "Hexadecimal string cannot be null.");
        }

        if (hexString.Length % 2 != 0)
        {
            throw new ArgumentException("Hexadecimal string must have an even length", nameof(hexString));
        }

        var bytes = new byte[hexString.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var currentHex = hexString.Substring(i * 2, 2);
            bytes[i] = Convert.ToByte(currentHex, 16);
        }

        return bytes;
    }
}
