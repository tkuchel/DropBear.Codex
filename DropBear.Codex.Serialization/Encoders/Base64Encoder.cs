#region

using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Encoders;

/// <summary>
///     Provides methods to encode and decode data using Base64 encoding.
/// </summary>
public class Base64Encoder : IEncoder
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Base64Encoder" /> class.
    /// </summary>
    public Base64Encoder()
    {
        _logger = LoggerFactory.Logger.ForContext<Base64Encoder>();
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
                var encoded = Encoding.UTF8.GetBytes(Convert.ToBase64String(data));
                return encoded;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during Base64 encoding");
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
                var base64EncodedString = Encoding.UTF8.GetString(encodedData);
                var decoded = Convert.FromBase64String(base64EncodedString);
                return decoded;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during Base64 decoding");
            throw;
        }
    }
}
